using BuildingBlocks.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.EntityFrameworkCore.Tests;

public sealed class AdoptionTests : IDisposable
{
    private readonly SqliteFixture db = new();

    [Theory]
    [InlineData(typeof(Order))]
    [InlineData(typeof(OrderLine))]
    [InlineData(typeof(Note))]
    public void TenantOwnedEntitiesGetFilterRequiredColumnAndIndex(Type clrType)
    {
        using var context = db.NewContext(null);
        var entity = context.Model.FindEntityType(clrType)!;
        var tenant = entity.FindProperty(nameof(ITenantOwned.TenantId))!;

        Assert.Contains(entity.GetDeclaredQueryFilters(), f => f.Key == TenantFilter.Name);
        Assert.False(tenant.IsNullable);
        Assert.Equal(TenantFilter.TenantIdMaxLength, tenant.GetMaxLength());
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Count == 1 && i.Properties[0] == tenant);
    }

    [Fact]
    public void NonTenantEntityIsUntouched()
    {
        using var context = db.NewContext(null);
        var entity = context.Model.FindEntityType(typeof(Country))!;

        Assert.DoesNotContain(entity.GetDeclaredQueryFilters(), f => f.Key == TenantFilter.Name);
        Assert.Null(entity.FindProperty(nameof(ITenantOwned.TenantId)));
    }

    public void Dispose() => db.Dispose();
}
