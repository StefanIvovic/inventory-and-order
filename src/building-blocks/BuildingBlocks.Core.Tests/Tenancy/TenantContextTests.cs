using BuildingBlocks.Core.Tenancy;

namespace BuildingBlocks.Core.Tests.Tenancy;

public sealed class TenantContextTests
{
    [Fact]
    public void StartsWithoutTenant()
    {
        var context = new TenantContext();

        Assert.False(context.HasTenant);
        Assert.Null(context.TenantId);
    }

    [Fact]
    public void SetOnceStoresTenant()
    {
        var context = new TenantContext();

        context.Set("acme");

        Assert.True(context.HasTenant);
        Assert.Equal("acme", context.TenantId);
    }

    [Fact]
    public void SettingSameTenantAgainIsNoOp()
    {
        var context = new TenantContext();
        context.Set("acme");

        context.Set("acme");

        Assert.Equal("acme", context.TenantId);
    }

    [Fact]
    public void SettingDifferentTenantFailsAndKeepsOriginal()
    {
        var context = new TenantContext();
        context.Set("acme");

        Assert.Throws<InvalidOperationException>(() => context.Set("globex"));
        Assert.Equal("acme", context.TenantId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SettingBlankTenantFails(string? tenantId)
    {
        var context = new TenantContext();

        Assert.ThrowsAny<ArgumentException>(() => context.Set(tenantId!));
        Assert.False(context.HasTenant);
    }
}
