using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests.Tenancy;

public sealed class TenantResolutionTests
{
    private const string UserId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

    [Fact]
    public async Task StaffRequestSeesItsTenant()
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get("/tenant", TestAuthHandler.Claims(UserId, "acme", "staff")), TestContext.Current.CancellationToken);

        Assert.Equal("acme", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PlatformRequestWithoutTenantHasNoTenant()
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get("/tenant", TestAuthHandler.Claims(UserId, null, "platform")), TestContext.Current.CancellationToken);

        Assert.Equal("<none>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentRequestsSeeOnlyTheirOwnTenant()
    {
        await using var app = await StartAsync();
        var cancellation = TestContext.Current.CancellationToken;

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(async index =>
        {
            var tenant = index % 2 == 0 ? "acme" : "globex";
            var response = await app.Client.SendAsync(Get("/tenant", TestAuthHandler.Claims(UserId, tenant, "staff")), cancellation);
            return (Expected: tenant, Actual: await response.Content.ReadAsStringAsync(cancellation));
        }));

        Assert.All(results, result => Assert.Equal(result.Expected, result.Actual));
    }
}
