namespace BuildingBlocks.EntityFrameworkCore;

public sealed class TenantViolationException(string message) : InvalidOperationException(message);
