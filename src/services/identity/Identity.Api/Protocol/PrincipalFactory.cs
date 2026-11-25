using System.Collections.Immutable;
using System.Security.Claims;
using BuildingBlocks.Core.Identity;
using Identity.Api.Users;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Api.Protocol;

public sealed class PrincipalFactory(IOpenIddictScopeManager scopes)
{
    public const string SecurityStampClaim = "identity_stamp";

    public static bool HasApiScope(IEnumerable<string> requested) => requested.Any(Protocol.Scopes.Api.Contains);

    public async Task<ClaimsPrincipal> CreateAsync(AppUser user, ImmutableArray<string> requestedScopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString());
        identity.SetClaim(AppClaimTypes.UserType, user.UserType);
        if (user.TenantId is not null)
        {
            identity.SetClaim(AppClaimTypes.TenantId, user.TenantId);
        }

        if (requestedScopes.Contains(Protocol.Scopes.Email) && !string.IsNullOrEmpty(user.Email))
        {
            identity.SetClaim(Claims.Email, user.Email);
        }

        identity.SetClaim(SecurityStampClaim, user.SecurityStamp);
        identity.SetScopes(requestedScopes);

        var resources = new List<string>();
        await foreach (var resource in scopes.ListResourcesAsync(identity.GetScopes(), cancellationToken))
        {
            resources.Add(resource);
        }

        identity.SetResources(resources);
        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            AppClaimTypes.UserType or AppClaimTypes.TenantId => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email => [Destinations.IdentityToken],
            _ => [],
        });

        return new ClaimsPrincipal(identity);
    }
}
