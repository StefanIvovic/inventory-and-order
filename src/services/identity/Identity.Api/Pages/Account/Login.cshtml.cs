using Identity.Api.Persistence;
using Identity.Api.Protocol;
using Identity.Api.Tenants;
using Identity.Api.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Identity.Api.Pages.Account;

public sealed class LoginModel(SignInManager<AppUser> signIn, IdentityDbContext db) : PageModel
{
    public const string InvalidCredentials = "The email or password is incorrect.";
    public const string LockedOut = "This account is temporarily locked. Try again later.";

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public string? TenantName { get; private set; }

    public string? RegisterUrl { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadTenantAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var tenant = await LoadTenantAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password) || (SignInTarget.From(ReturnUrl).IsCustomer && tenant is null))
        {
            ErrorMessage = InvalidCredentials;
            return Page();
        }

        var email = Email.Trim();
        var userName = tenant is null ? email : AppUser.CustomerUserName(tenant.Id, email);
        var result = await signIn.PasswordSignInAsync(userName, Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return ReturnUrl is not null && Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Account/SignedIn");
        }

        ErrorMessage = result.IsLockedOut ? LockedOut : InvalidCredentials;
        return Page();
    }

    private async Task<Tenant?> LoadTenantAsync(CancellationToken cancellationToken)
    {
        var target = SignInTarget.From(ReturnUrl);
        if (!target.IsCustomer)
        {
            return null;
        }

        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == target.TenantSlug, cancellationToken);
        TenantName = tenant?.Name;
        RegisterUrl = tenant is null ? null : "/account/register?returnUrl=" + Uri.EscapeDataString(ReturnUrl!);
        return tenant;
    }
}
