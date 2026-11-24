namespace BuildingBlocks.Validation.Tests;

public sealed class FieldNamesTests
{
    [Theory]
    [InlineData("Name", "name")]
    [InlineData("WarehouseId", "warehouseId")]
    [InlineData("Lines[1].Sku", "lines[1].sku")]
    [InlineData("Address.PostalCode", "address.postalCode")]
    [InlineData("alreadyCamel", "alreadyCamel")]
    [InlineData("", "")]
    public void ConvertsToClientCamelCasePaths(string propertyName, string expected)
    {
        Assert.Equal(expected, FieldNames.ToClientName(propertyName));
    }
}
