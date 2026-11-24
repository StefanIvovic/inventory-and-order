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
            else if (!await IsCurrentAsync(manager, existing, descriptor, cancellationToken))
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
        OpenIddictApplicationDescriptor wanted,
        CancellationToken cancellationToken)
    {
        var current = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(current, application, cancellationToken);
        return string.Equals(current.ClientType, wanted.ClientType, StringComparison.Ordinal)
            && string.Equals(current.ConsentType, wanted.ConsentType, StringComparison.Ordinal)
            && current.RedirectUris.SetEquals(wanted.RedirectUris)
            && current.PostLogoutRedirectUris.SetEquals(wanted.PostLogoutRedirectUris)
            && current.Permissions.SetEquals(wanted.Permissions)
            && current.Requirements.SetEquals(wanted.Requirements)
            && await manager.ValidateClientSecretAsync(application, wanted.ClientSecret!, cancellationToken);
    }

    private static string Require(string? value, string clientId, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Identity:Clients:{clientId}:{name} is required.")
            : value;
}
