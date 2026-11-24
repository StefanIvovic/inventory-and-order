using System.Net;
using BuildingBlocks.Testing;
using Identity.Api.Authorization;

namespace Identity.Tests.Flow;

public sealed class OwnTokenTests(ContainersFixture containers)
{
    [Fact]
    public async Task StaffWithRoleUsesARealTokenOnRoles()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var staff = await app.CreateUserAsync("staff@acme.test", "Staff#Pass1", "acme");
        await app.AssignAsync("acme", staff.Id, IdentityRoles.Administrator);

        var tokens = await CodeFlow.SignInAsync(app, "staff@acme.test", "Staff#Pass1");
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/roles", tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task ProtocolAndLoginEndpointsAreAllowListed()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var client = app.CreateClient();

        var report = await NoRolesRouteCheck.RunAsync(app.Services, client, IdentityApp.Audience, TestContext.Current.CancellationToken);

        Assert.True(report.Failures.Count == 0, report.ToString());
        Assert.Contains(report.Skipped, r => r.Contains("/connect/authorize", StringComparison.Ordinal));
        Assert.Contains(report.Skipped, r => r.Contains("/connect/token", StringComparison.Ordinal));
        Assert.Contains(report.Skipped, r => r.Contains("account/login", StringComparison.Ordinal));
        Assert.Contains(report.Skipped, r => r.Contains("connect/endsession", StringComparison.Ordinal));
    }
}
