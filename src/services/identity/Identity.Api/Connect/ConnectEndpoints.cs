using Identity.Api.Persistence;
using Identity.Api.Protocol;
using Identity.Api.Users;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Api.Connect;

public static class ConnectEndpoints
{
    public const string AuthorizePath = "/connect/authorize";
    public const string TokenPath = "/connect/token";
    public const string EndSessionPath = "/connect/endsession";

    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapMethods(AuthorizePath, [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync).AllowAnonymous();
        endpoints.MapPost(TokenPath, TokenAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> AuthorizeAsync(
        HttpContext context,
        UserManager<AppUser> users,
        PrincipalFactory principals,
        CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (!PrincipalFactory.HasApiScope(request.GetScopes()))
        {
            return Reject(Errors.InvalidScope, "At least one API scope is required.");
        }

        var session = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = session.Succeeded ? await users.GetUserAsync(session.Principal!) : null;
        if (user is null)
        {
            var parameters = context.Request.HasFormContentType ? context.Request.Form.ToList() : context.Request.Query.ToList();
            var returnUrl = context.Request.PathBase + context.Request.Path + QueryString.Create(parameters);
            return Results.Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, [IdentityConstants.ApplicationScheme]);
        }

        if (await users.IsLockedOutAsync(user))
        {
            return Reject(Errors.AccessDenied, "The account is locked.");
        }

        var principal = await principals.CreateAsync(user, request.GetScopes(), cancellationToken);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> TokenAsync(
        HttpContext context,
        UserManager<AppUser> users,
        PrincipalFactory principals,
        IdentityDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return Reject(Errors.UnsupportedGrantType, "The grant type is not supported.");
        }

        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var stored = result.Principal;
        var subject = stored?.GetClaim(Claims.Subject);
        var user = subject is null ? null : await users.FindByIdAsync(subject);
        if (stored is null
            || user is null
            || await users.IsLockedOutAsync(user)
            || !string.Equals(stored.GetClaim(PrincipalFactory.SecurityStampClaim), user.SecurityStamp, StringComparison.Ordinal))
        {
            return Reject(Errors.InvalidGrant, "The token is no longer valid.");
        }

        if (request.IsRefreshTokenGrantType() && !await ClaimRefreshTokenAsync(db, stored.GetTokenId(), clock, cancellationToken))
        {
            return Reject(Errors.InvalidGrant, "The token is no longer valid.");
        }

        var principal = await principals.CreateAsync(user, stored.GetScopes(), cancellationToken);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<bool> ClaimRefreshTokenAsync(IdentityDbContext db, string? tokenId, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tokenId, out var id))
        {
            return false;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var claimed = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE openiddict_tokens SET \"Status\" = {Statuses.Redeemed}, \"RedemptionDate\" = {now} WHERE \"Id\" = {id} AND \"Status\" = {Statuses.Valid}",
            cancellationToken);
        return claimed == 1;
    }

    private static IResult Reject(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
