using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Identity.Api.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Protocol;

public static class PlatformAdminSeeder
{
    public static async Task SeedAsync(
        UserManager<AppUser> users,
        IdentityDbContext db,
        PlatformAdminSettings settings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);

        if (await users.Users.AnyAsync(u => u.UserType == UserTypes.Platform, cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException(
                "No platform admin exists. Identity:PlatformAdmin:Email and Identity:PlatformAdmin:Password are required on first start.");
        }

        var now = clock.GetUtcNow();
        var admin = AppUser.Platform(settings.Email, now);
        var result = await users.CreateAsync(admin, settings.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "The platform admin could not be created: " + string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        db.PlatformRoleAssignments.Add(PlatformRoleAssignment.Create(admin.Id, IdentityRoles.PlatformAdministrator, now));
        await db.SaveChangesAsync(cancellationToken);
    }
}
