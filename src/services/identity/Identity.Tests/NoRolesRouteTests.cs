using BuildingBlocks.Testing;

namespace Identity.Tests;

public sealed class NoRolesRouteTests(ContainersFixture containers)
{
    [Fact]
    public async Task EveryRouteDeniesAUserWithoutRoles()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        await app.WaitUntilMigratedAsync();

        var report = await NoRolesRouteCheck.RunAsync(app.Services, client, IdentityApp.Audience, TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());

        Assert.True(report.Failures.Count == 0, report.ToString());
        Assert.Equal(
            [
                "DELETE /users/{id:guid}/roles/{role} (Tenant)",
                "GET /roles (Tenant)",
                "GET /tenants (Platform)",
                "GET /users (Tenant)",
                "POST /tenants (Platform)",
                "POST /users (Tenant)",
                "POST /users/{id:guid}/disable (Tenant)",
                "PUT /users/{id:guid}/roles/{role} (Tenant)",
            ],
            report.Checked.Order(StringComparer.Ordinal));
        Assert.Equal(["GET /lookups/{typeCode}/values"], report.SignedIn);
        Assert.Contains("GET /health/live", report.Skipped);
        Assert.Contains("GET /health/ready", report.Skipped);
    }
}
