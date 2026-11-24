using Identity.Api.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Identity.Api.Pages.Account;

public sealed class LoginModel(SignInManager<AppUser> signIn) : PageModel
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

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = InvalidCredentials;
            return Page();
        }

        var result = await signIn.PasswordSignInAsync(Email.Trim(), Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return ReturnUrl is not null && Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Account/SignedIn");
        }

        ErrorMessage = result.IsLockedOut ? LockedOut : InvalidCredentials;
        return Page();
    }
}
