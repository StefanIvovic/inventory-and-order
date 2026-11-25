namespace Identity.Api.Features.CreateTenant;

public sealed record CreateTenant(string Slug, string Name, string AdminEmail, string AdminPassword);

public sealed record CreateTenantResponse(string Id, string Slug, Guid AdminUserId);
