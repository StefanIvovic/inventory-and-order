using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Identity.Api.Pages.Connect;

public sealed class EndSessionModel(IOpenIddictApplicationManager applications, IOpenIddictTokenManager tokens) : PageModel
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
            var client = request?.ClientId is { } clientId ? await applications.FindByClientIdAsync(clientId, cancellationToken) : null;
            var clientKey = client is null ? null : await applications.GetIdAsync(client, cancellationToken);
            _ = clientKey is null
                ? await tokens.RevokeBySubjectAsync(subject, cancellationToken)
                : await tokens.RevokeAsync(subject, clientKey, status: null, type: null, cancellationToken);
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return SignOut(new AuthenticationProperties { RedirectUri = "/account/signed-out" }, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
