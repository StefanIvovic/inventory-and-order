using JasperFx;

namespace BuildingBlocks.Messaging;

internal static class EnvelopeTenancy
{
    public static string? Normalize(string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId) || tenantId == StorageConstants.DefaultTenantId ? null : tenantId;
}
