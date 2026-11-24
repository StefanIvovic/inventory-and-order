using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Api.Protocol;

public static class ClientRegistrar
{
    public static IReadOnlyList<string> GatewayClients { get; } = ["backoffice-bff", "storefront-bff"];

    public static async Task RegisterAsync(IOpenIddictApplicationManager manager, ProtocolSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(settings);

        foreach (var clientId in GatewayClients)
        {
            var client = settings.Clients.GetValueOrDefault(clientId)
                ?? throw new InvalidOperationException($"Identity:Clients:{clientId} is required.");
            var secret = Require(client.Secret, clientId, nameof(ClientSettings.Secret));
            var redirect = new Uri(Require(client.RedirectUri, clientId, nameof(ClientSettings.RedirectUri)), UriKind.Absolute);
            var postLogout = new Uri(Require(client.PostLogoutRedirectUri, clientId, nameof(ClientSettings.PostLogoutRedirectUri)), UriKind.Absolute);
            var descriptor = Describe(clientId, secret, redirect, postLogout);

            var existing = await manager.FindByClientIdAsync(clientId, cancellationToken);
            if (existing is null)
            {
                await manager.CreateAsync(descriptor, cancellationToken);
            }
            else if (!await IsCurrentAsync(manager, existing, secret, redirect, postLogout, cancellationToken))
            {
                await manager.UpdateAsync(existing, descriptor, cancellationToken);
            }
        }
    }

    private static OpenIddictApplicationDescriptor Describe(string clientId, string secret, Uri redirect, Uri postLogout)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = clientId,
            RedirectUris = { redirect },
            PostLogoutRedirectUris = { postLogout },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        foreach (var scope in Scopes.Api)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        return descriptor;
    }

    private static async Task<bool> IsCurrentAsync(
        IOpenIddictApplicationManager manager,
        object application,
        string secret,
        Uri redirect,
        Uri postLogout,
        CancellationToken cancellationToken)
    {
        var redirects = await manager.GetRedirectUrisAsync(application, cancellationToken);
        var postLogouts = await manager.GetPostLogoutRedirectUrisAsync(application, cancellationToken);
        return redirects.SequenceEqual([redirect.AbsoluteUri], StringComparer.Ordinal)
            && postLogouts.SequenceEqual([postLogout.AbsoluteUri], StringComparer.Ordinal)
            && await manager.ValidateClientSecretAsync(application, secret, cancellationToken);
    }

    private static string Require(string? value, string clientId, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Identity:Clients:{clientId}:{name} is required.")
            : value;
}
