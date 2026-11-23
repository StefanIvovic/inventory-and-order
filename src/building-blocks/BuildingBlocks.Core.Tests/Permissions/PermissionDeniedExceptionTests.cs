using BuildingBlocks.Core.Permissions;

namespace BuildingBlocks.Core.Tests.Permissions;

public sealed class PermissionDeniedExceptionTests
{
    [Fact]
    public void KeepsMissingInRequestedOrderWithoutDuplicates()
    {
        var exception = new PermissionDeniedException(["orders.cancel", "orders.refund", "orders.cancel"]);

        Assert.Equal(["orders.cancel", "orders.refund"], exception.Missing);
        Assert.Equal("permission-denied", exception.Code);
    }

    [Fact]
    public void EmptyMissingListIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new PermissionDeniedException([]));
    }
}
