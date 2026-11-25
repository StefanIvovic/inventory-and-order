namespace Identity.Contracts;

public sealed record UserRegistered(Guid UserId, string TenantId, string UserType, string Email);
