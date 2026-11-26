namespace BuildingBlocks.Lookups.Tests;

public sealed class LookupCatalogTests
{
    private static readonly Guid A = Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static readonly Guid B = Guid.Parse("20000000-0000-4000-8000-000000000002");
    private static readonly Guid C = Guid.Parse("20000000-0000-4000-8000-000000000003");

    [Fact]
    public void ValidCatalogIsAccepted()
    {
        var catalog = Catalogs.Colors();

        Assert.Equal(["color", "size"], catalog.Types.Select(t => t.Code));
    }

    [Fact]
    public void RepeatedIdIsRefused()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new LookupCatalog(new LookupTypeDefinition(A, "color", "Color", new LookupValueDefinition(A, "red", "Red"))));

        Assert.Contains("repeated id", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Red")]
    [InlineData("red_one")]
    [InlineData("-red")]
    [InlineData("red-")]
    [InlineData("")]
    public void BadCodeIsRefused(string code)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new LookupCatalog(new LookupTypeDefinition(A, "color", "Color", new LookupValueDefinition(B, code, "Red"))));

        Assert.Contains("lower-case words", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CodeLongerThanTheLimitIsRefused()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new LookupCatalog(new LookupTypeDefinition(A, new string('a', LookupType.CodeMaxLength + 1), "Color")));

        Assert.Contains("lower-case words", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedValueCodeInOneTypeIsRefused()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new LookupCatalog(new LookupTypeDefinition(
            A, "color", "Color", new LookupValueDefinition(B, "red", "Red"), new LookupValueDefinition(C, "red", "Crimson"))));

        Assert.Contains("used twice in 'color'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameValueCodeInTwoTypesIsAllowed()
    {
        var catalog = new LookupCatalog(
            new LookupTypeDefinition(A, "color", "Color", new LookupValueDefinition(B, "other", "Other")),
            new LookupTypeDefinition(C, "size", "Size", new LookupValueDefinition(Guid.Parse("20000000-0000-4000-8000-000000000004"), "other", "Other")));

        Assert.Equal(2, catalog.Types.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingNameIsRefused(string name)
    {
        var error = Assert.Throws<InvalidOperationException>(() => new LookupCatalog(new LookupTypeDefinition(A, "color", name)));

        Assert.Contains("needs a name", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NameLongerThanTheLimitIsRefused()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new LookupCatalog(new LookupTypeDefinition(
            A, "color", "Color", new LookupValueDefinition(B, "red", new string('r', LookupType.NameMaxLength + 1)))));

        Assert.Contains("needs a name", error.Message, StringComparison.Ordinal);
    }
}
