namespace Identity.Api.Features.ListTenants;

public sealed record ListTenants;

public sealed record TenantItem(string Id, string Slug, string Name, DateTimeOffset CreatedAt);

public sealed record TenantListActions(bool CanCreate);

public sealed record TenantList(IReadOnlyList<TenantItem> Items, TenantListActions Actions);
