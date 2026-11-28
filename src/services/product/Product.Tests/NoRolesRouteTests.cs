using BuildingBlocks.Testing;

namespace Product.Tests;

public sealed class NoRolesRouteTests(ContainersFixture containers)
{
    [Fact]
    public async Task EveryRouteDeniesAUserWithoutRoles()
    {
        await using var app = await ProductApp.StartAsync(containers);
        using var client = app.CreateClient();
        await app.WaitUntilMigratedAsync();

        var report = await NoRolesRouteCheck.RunAsync(app.Services, client, ProductApp.Audience, TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());

        Assert.True(report.Failures.Count == 0, report.ToString());
        Assert.Equal(["GET /roles (Tenant)"], report.Checked.Order(StringComparer.Ordinal));
        Assert.Empty(report.SignedIn);
        Assert.Contains("GET /health/live", report.Skipped);
        Assert.Contains("GET /health/ready", report.Skipped);
    }
}
