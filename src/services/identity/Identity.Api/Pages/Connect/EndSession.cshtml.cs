using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Api.Pages.Connect;

public sealed class EndSessionModel(
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens) : PageModel
{
    public IReadOnlyList<KeyValuePair<string, string>> Fields { get; private set; } = [];

    public void OnGet()
    {
        Fields = Request.Query.Select(f => new KeyValuePair<string, string>(f.Key, f.Value.ToString())).ToList();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        var session = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var subject = session.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (subject is not null)
        {
            var hint = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var authorizationId = hint.Principal?.GetClaim(Claims.Subject) == subject ? hint.Principal.GetAuthorizationId() : null;
            if (authorizationId is not null)
            {
                await tokens.RevokeByAuthorizationIdAsync(authorizationId, cancellationToken);
                if (await authorizations.FindByIdAsync(authorizationId, cancellationToken) is { } authorization)
                {
                    await authorizations.TryRevokeAsync(authorization, cancellationToken);
                }
            }
            else if (request?.ClientId is { } clientId && await applications.FindByClientIdAsync(clientId, cancellationToken) is { } client)
            {
                await tokens.RevokeAsync(subject, (await applications.GetIdAsync(client, cancellationToken))!, status: null, type: null, cancellationToken);
            }
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return SignOut(new AuthenticationProperties { RedirectUri = "/account/signed-out" }, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
