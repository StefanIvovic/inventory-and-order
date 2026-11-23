using BuildingBlocks.Core.Identity;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace BuildingBlocks.Messaging;

internal sealed class IdentityEnvelopeRule(IHttpContextAccessor accessor) : IEnvelopeRule
{
    public void Modify(Envelope envelope) => ApplyFromHttp(envelope);

    public void ApplyCorrelation(IMessageContext originator, Envelope outgoing)
    {
        if (originator.Envelope is { } incoming)
        {
            outgoing.TenantId = EnvelopeTenancy.Normalize(outgoing.TenantId) ?? EnvelopeTenancy.Normalize(incoming.TenantId);
            if (!outgoing.Headers.ContainsKey(MessageHeaders.UserId) &&
                incoming.Headers.TryGetValue(MessageHeaders.UserId, out var userId))
            {
                outgoing.Headers[MessageHeaders.UserId] = userId;
            }
        }

        ApplyFromHttp(outgoing);
    }

    private void ApplyFromHttp(Envelope envelope)
    {
        var user = accessor.HttpContext?.Features.Get<CurrentUser>();
        if (user is null)
        {
            return;
        }

        envelope.TenantId = EnvelopeTenancy.Normalize(envelope.TenantId) ?? user.TenantId;
        if (!envelope.Headers.ContainsKey(MessageHeaders.UserId))
        {
            envelope.Headers[MessageHeaders.UserId] = user.UserId.ToString();
        }
    }
}
