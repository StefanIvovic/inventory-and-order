namespace Identity.Contracts;

public sealed record TenantCreated(string TenantId, Guid AdminUserId);
