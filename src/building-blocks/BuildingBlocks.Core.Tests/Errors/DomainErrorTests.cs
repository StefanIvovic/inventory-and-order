using BuildingBlocks.Core.Errors;

namespace BuildingBlocks.Core.Tests.Errors;

public sealed class DomainErrorTests
{
    [Theory]
    [InlineData(DomainErrorKind.Conflict)]
    [InlineData(DomainErrorKind.Rule)]
    public void DomainExceptionKeepsCodeKindAndMessage(DomainErrorKind kind)
    {
        var exception = new DomainException("inventory.transfer.insufficient-stock", kind, "Not enough stock.");

        Assert.Equal("inventory.transfer.insufficient-stock", exception.Code);
        Assert.Equal(kind, exception.Kind);
        Assert.Equal("Not enough stock.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void DomainExceptionRequiresCode(string? code)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DomainException(code!, DomainErrorKind.Rule, "x"));
    }

    [Fact]
    public void NotFoundExceptionHasFixedCodeAndGenericMessage()
    {
        var exception = new NotFoundException("order");

        Assert.Equal("not-found", exception.Code);
        Assert.Equal("order", exception.Resource);
        Assert.Equal("The requested order was not found.", exception.Message);
    }

    [Fact]
    public void NotFoundExceptionCanCarryASpecificCode()
    {
        var exception = new NotFoundException("lookup type", "lookup-type-not-found");

        Assert.Equal("lookup-type-not-found", exception.Code);
        Assert.Equal("The requested lookup type was not found.", exception.Message);
        Assert.ThrowsAny<ArgumentException>(() => new NotFoundException("order", " "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NotFoundExceptionRequiresResource(string? resource)
    {
        Assert.ThrowsAny<ArgumentException>(() => new NotFoundException(resource!));
    }
}
