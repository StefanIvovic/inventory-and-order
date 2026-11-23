using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Tenancy;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace BuildingBlocks.Messaging;

public static class TenantContextMiddleware
{
    public static void Before(Envelope envelope, TenantContext tenantContext, IHttpContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(accessor);

        var tenantId = EnvelopeTenancy.Normalize(envelope.TenantId) ?? accessor.HttpContext?.Features.Get<CurrentUser>()?.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            tenantContext.Set(tenantId);
        }
    }
}
