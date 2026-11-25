using Gateway.Api.Configuration;

namespace Gateway.Api.Sessions;

public sealed class IdentityBackchannelHandler(GatewaySettings settings) : DelegatingHandler
{
    public const string ClientName = "identity";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var origin = settings.Origin;
        if (request.RequestUri is { IsAbsoluteUri: true } target && IsSameOrigin(target, origin))
        {
            var identity = settings.IdentityAddress;
            request.RequestUri = new UriBuilder(target) { Scheme = identity.Scheme, Host = identity.Host, Port = identity.Port }.Uri;
            request.Headers.Remove("X-Forwarded-Proto");
            request.Headers.Remove("X-Forwarded-Host");
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", origin.Scheme);
            request.Headers.TryAddWithoutValidation("X-Forwarded-Host", origin.Authority);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static bool IsSameOrigin(Uri target, Uri origin) =>
        string.Equals(target.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(target.Host, origin.Host, StringComparison.OrdinalIgnoreCase)
        && target.Port == origin.Port;
}
