using Identity.Api.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Protocol;

public static class PlatformAdminSeeder
{
    public static async Task SeedAsync(UserManager<AppUser> users, PlatformAdminSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(settings);

        if (await users.Users.AnyAsync(u => u.UserType == UserTypes.Platform, cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException(
                "No platform admin exists. Identity:PlatformAdmin:Email and Identity:PlatformAdmin:Password are required on first start.");
        }

        var result = await users.CreateAsync(AppUser.Platform(settings.Email), settings.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "The platform admin could not be created: " + string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }
}
