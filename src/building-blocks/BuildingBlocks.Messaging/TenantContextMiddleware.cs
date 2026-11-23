using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Tenancy;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace BuildingBlocks.Messaging;

public static class TenantContextMiddleware
{
    public static void Before(Envelope envelope, ITenantContext tenantContext, IHttpContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(accessor);

        var tenantId = EnvelopeTenancy.Normalize(envelope.TenantId) ?? accessor.HttpContext?.Features.Get<CurrentUser>()?.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var settable = tenantContext as TenantContext
                ?? throw new InvalidOperationException($"{nameof(ITenantContext)} must resolve to {nameof(TenantContext)} to be set from message metadata.");
            settable.Set(tenantId);
        }
    }
}
