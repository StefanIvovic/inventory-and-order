namespace BuildingBlocks.AspNetCore.Authentication;

public sealed class IssuerBackchannelHandler(Uri authority, Uri internalAddress) : DelegatingHandler
{
    private const string ForwardedProto = "X-Forwarded-Proto";
    private const string ForwardedHost = "X-Forwarded-Host";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestUri is { IsAbsoluteUri: true } target && IsAuthorityOrigin(target))
        {
            request.RequestUri = new UriBuilder(target)
            {
                Scheme = internalAddress.Scheme,
                Host = internalAddress.Host,
                Port = internalAddress.Port,
            }.Uri;
            request.Headers.Remove(ForwardedProto);
            request.Headers.Remove(ForwardedHost);
            request.Headers.TryAddWithoutValidation(ForwardedProto, authority.Scheme);
            request.Headers.TryAddWithoutValidation(ForwardedHost, authority.Authority);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private bool IsAuthorityOrigin(Uri target) =>
        string.Equals(target.Scheme, authority.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(target.Host, authority.Host, StringComparison.OrdinalIgnoreCase)
        && target.Port == authority.Port;
}
