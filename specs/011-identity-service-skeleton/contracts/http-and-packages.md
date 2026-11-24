# Contracts: Identity Service Skeleton

## HTTP (Identity API, port 8080 in the container)

| Method | Path | Category | Success | Errors |
|---|---|---|---|---|
| GET | `/health/live` | allow-listed | 200 | none |
| GET | `/health/ready` | allow-listed | 200 | 503 while the database is unreachable or migrations are pending |
| GET | `/roles` | tenant | 200 `RolesResponse` | 401 no or invalid token, 401 `invalid-identity`, 403 platform caller, 403 missing `identity.roles.view` |

`GET /roles` 200:
```json
{ "roles": [ { "name": "identity.administrator", "permissions": ["identity.roles.view"] } ], "canAssign": false }
```
403 (missing permission) is the existing BuildingBlocks problem-details shape, listing the missing permissions.

## BuildingBlocks.AspNetCore (new public API, released as 0.1.0)

```csharp
IServiceCollection AddBuildingBlocksAuthentication(this IServiceCollection services, IConfiguration configuration);
IApplicationBuilder UseBuildingBlocksAuthentication(this IApplicationBuilder app);
TBuilder RequirePlatform<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder;
sealed class PlatformOnlyAttribute : Attribute, IAuthorizeData;
static class AuthorizationPolicies { const string Tenant = "tenant"; const string Platform = "platform"; }
```

The configuration section is `Authentication`, with `Authority` (required), `Audience` (required) and `RequireHttpsMetadata` (default true). `UseBuildingBlocksAuthentication` replaces separate `UseAuthentication` + `UseBuildingBlocks` + `UseAuthorization` calls.

## BuildingBlocks.Core (behaviour change)

`CurrentUser.TryCreate` rejects `user_type=platform` with a `tenant_id` claim.

## BuildingBlocks.Testing (new public API)

```csharp
static class TestTokens {
  const string Issuer; const string DefaultAudience;
  string For(Guid userId, string tenantId, UserType userType = UserType.Staff, string? audience = null);
  string Platform(Guid userId, string? audience = null);
  string Raw(IDictionary<string, object> claims, DateTimeOffset? expires = null, bool signWithUnknownKey = false);
}
IServiceCollection AddTestTokenValidation(this IServiceCollection services, string? audience = null);
static class NoRolesRouteCheck {
  Task<RouteCheckReport> RunAsync(IServiceProvider services, HttpClient client, string? audience = null, CancellationToken ct = default);
}
sealed record RouteCheckReport(IReadOnlyList<string> Checked, IReadOnlyList<string> Skipped, IReadOnlyList<string> Failures);
```

## Scripts

- `scripts/restore-feed.ps1 -Service <name>`: packs the BuildingBlocks version pinned by `src/services/<name>/Directory.Packages.props` from its tag into `artifacts/packages`. Exits 1 if the pin is a prerelease or the tag is missing.
- `scripts/dev.ps1`: unchanged. `-Profile infra,apps` starts Identity.
- `scripts/codegen.ps1 -Service <name> [-Check]`: deletes and regenerates the Wolverine code for a service (placeholder configuration, no database needed). `-Check` fails if the result differs from what is committed.
- `UseBuildingBlocks()` now also adds an inner exception handler, so mapped errors keep their status in the Development environment, where ASP.NET's developer exception page would otherwise catch them first.
