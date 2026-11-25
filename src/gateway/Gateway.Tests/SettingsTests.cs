namespace Gateway.Tests;

public sealed class SettingsTests
{
    [Theory]
    [InlineData("Gateway:PublicOrigin", "gateway.test")]
    [InlineData("Gateway:PublicOrigin", "https://gateway.test/sub/")]
    [InlineData("Gateway:PublicOrigin", "ftp://gateway.test/")]
    [InlineData("Gateway:Identity:InternalAddress", "identity-api")]
    [InlineData("Gateway:Clients:backoffice-bff:Secret", "")]
    [InlineData("Gateway:Clients:storefront-bff:Secret", " ")]
    [InlineData("Gateway:Refresh:Threshold", "00:00:00")]
    [InlineData("Gateway:Refresh:Threshold", "00:20:00")]
    public async Task InvalidSettingStopsTheHost(string key, string value)
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var app = await GatewayApp.StartAsync(overrides: new Dictionary<string, string?> { [key] = value });
        });

        Assert.Contains(key.Split(':')[1], error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeysPathIsRequiredInProduction()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var app = await GatewayApp.StartAsync(overrides: new Dictionary<string, string?> { ["environment"] = "Production" });
        });

        Assert.Contains("Gateway:Keys:Path", error.ToString(), StringComparison.Ordinal);
    }
}
