using BuildingBlocks.Core.Errors;
using FluentValidation;
using Identity.Api.Features.RegisterCustomer;
using Identity.Api.Persistence;
using Identity.Api.Protocol;
using Identity.Api.Tenants;
using Identity.Api.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wolverine;

namespace Identity.Api.Pages.Account;

public sealed class RegisterModel(
    IMessageBus bus,
    IdentityDbContext db,
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn) : PageModel
{
    public const string AlreadyExists = "An account with this email already exists.";
    public const string NoShop = "This shop does not exist.";

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true, Name = "tenant")]
    public string? TenantSlug { get; set; }

    public string? TenantName { get; private set; }

    public string? Message { get; private set; }

    public IReadOnlyDictionary<string, string[]> Errors { get; private set; } = new Dictionary<string, string[]>();

    public string LoginUrl => IsLocal(ReturnUrl) ? "/account/login?returnUrl=" + Uri.EscapeDataString(ReturnUrl!) : "/account/login";

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadTenantAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var tenant = await LoadTenantAsync(cancellationToken);
        if (tenant is null)
        {
            return Page();
        }

        try
        {
            var registered = await bus.InvokeAsync<RegisteredCustomer>(
                new RegisterCustomer(tenant.Id, Email ?? string.Empty, Password ?? string.Empty, ConfirmPassword ?? string.Empty),
                cancellationToken);
            var customer = await users.FindByIdAsync(registered.UserId.ToString());
            await signIn.SignInAsync(customer!, isPersistent: false);
            return IsLocal(ReturnUrl) ? LocalRedirect(ReturnUrl!) : RedirectToPage("/Account/SignedIn");
        }
        catch (ValidationException validation)
        {
            Errors = validation.Errors
                .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..], StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        }
        catch (DomainException conflict) when (conflict.Code == IdentityResultErrors.EmailTaken)
        {
            Message = AlreadyExists;
        }
        catch (DbUpdateException duplicate) when (IsCustomerEmailTaken(duplicate))
        {
            Message = AlreadyExists;
        }

        return Page();
    }

    public string? ErrorFor(string field) => Errors.TryGetValue(field, out var messages) ? string.Join(" ", messages) : null;

    private static bool IsCustomerEmailTaken(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: IdentityDbContext.CustomerEmailIndex or IdentityDbContext.UserNameIndex })
            {
                return true;
            }
        }

        return false;
    }

    private bool IsLocal(string? url) => url is not null && Url.IsLocalUrl(url);

    private async Task<Tenant?> LoadTenantAsync(CancellationToken cancellationToken)
    {
        var slug = string.IsNullOrWhiteSpace(TenantSlug) ? SignInTarget.From(ReturnUrl).TenantSlug : TenantSlug;
        var tenant = string.IsNullOrWhiteSpace(slug) ? null : await db.Tenants.SingleOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        TenantName = tenant?.Name;
        if (tenant is null)
        {
            Message = NoShop;
        }

        return tenant;
    }
}
