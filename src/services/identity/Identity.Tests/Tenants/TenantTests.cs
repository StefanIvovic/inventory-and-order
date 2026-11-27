using Identity.Api.Tenants;

namespace Identity.Tests.Tenants;

public sealed class TenantTests
{
    [Theory]
    [InlineData("Acme ltd", "ACME LTD")]
    [InlineData("  Globex  ", "GLOBEX")]
    [InlineData("istanbul Iş", "ISTANBUL IŞ")]
    public void CreateKeepsTheNameUpperCasedForSearch(string name, string normalized)
    {
        var tenant = Tenant.Create("acme", name, DateTimeOffset.UtcNow);

        Assert.Equal(normalized, tenant.NormalizedName);
    }
}
