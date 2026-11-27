using BuildingBlocks.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Product.Api.Persistence;

namespace Product.Tests;

public sealed class RoleAssignmentTableTests(ContainersFixture containers)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OneRowPerTenantUserAndRole()
    {
        await using var app = await MigratedAsync();
        var user = Guid.NewGuid();
        await app.AssignAsync("acme", user, "product.viewer");

        var error = await Assert.ThrowsAsync<PostgresException>(() => app.AssignAsync("acme", user, "product.viewer"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        await app.AssignAsync("globex", user, "product.viewer");
        await app.AssignAsync("acme", user, "product.administrator");
    }

    [Fact]
    public async Task TheDatabaseRefusesAnUnknownRole()
    {
        await using var app = await MigratedAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => app.AssignAsync("acme", Guid.NewGuid(), "identity.viewer"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task EachTenantSeesOnlyItsOwnAssignments()
    {
        await using var app = await MigratedAsync();
        var user = Guid.NewGuid();
        await app.AssignAsync("acme", user, "product.viewer");
        await app.AssignAsync("globex", user, "product.administrator");

        var tenant = new TenantContext();
        tenant.Set("acme");
        await using var db = new ProductDbContext(new DbContextOptionsBuilder<ProductDbContext>().UseNpgsql(app.Database).Options, tenant);

        var roles = await db.RoleAssignments.Select(a => a.Role).ToListAsync(Cancellation);

        Assert.Equal(["product.viewer"], roles);
    }

    private async Task<ProductApp> MigratedAsync()
    {
        var app = await ProductApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        return app;
    }
}
