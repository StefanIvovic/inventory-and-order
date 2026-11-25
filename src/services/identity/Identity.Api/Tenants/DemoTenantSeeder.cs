using Identity.Api.Persistence;
using Identity.Api.Protocol;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Identity.Api.Tenants;

public static class DemoTenantSeeder
{
    public static async Task<bool> SeedAsync(IServiceProvider services, DemoTenantSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsConfigured)
        {
            return false;
        }

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var outbox = provider.GetRequiredService<IDbContextOutbox<IdentityDbContext>>();
        if (await outbox.DbContext.Tenants.AnyAsync(t => t.Slug == settings.Slug, cancellationToken))
        {
            return false;
        }

        var created = await provider.GetRequiredService<TenantCreation>().CreateAsync(
            settings.Slug!,
            settings.Name!,
            settings.AdminEmail!,
            settings.AdminPassword!,
            provider.GetRequiredService<TimeProvider>().GetUtcNow(),
            nameof(DemoTenantSettings.AdminEmail),
            nameof(DemoTenantSettings.AdminPassword),
            cancellationToken);

        var delivery = new DeliveryOptions { TenantId = created.Tenant.Id };
        await outbox.PublishAsync(created.Created, delivery);
        await outbox.PublishAsync(created.Registered, delivery);
        await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
        return true;
    }
}
