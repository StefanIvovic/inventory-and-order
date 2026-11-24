# Research: Identity Service Skeleton

## R1. How a service gets a pinned BuildingBlocks version

- **Problem**: There are no tags yet, so every BuildingBlocks pack gets the version `0.0.0-alpha.0.<height>`, and the height changes with every commit in the repo. A service can't pin that version in a stable way. CI packing BuildingBlocks from `HEAD` gives a different version from the pinned one.
- **Decision**:
  - The BuildingBlocks changes in this feature are released with the tag `building-blocks/v0.1.0`, placed on the commit that finishes them. PRs merge with merge commits, so the tagged commit stays reachable from master.
  - Identity pins `0.1.0` in `src/services/identity/Directory.Packages.props`.
  - A new script, `scripts/restore-feed.ps1 -Service identity`, reads the pinned `BuildingBlocks.*` version, checks out `building-blocks/v<version>` into a temporary git worktree, packs it into `artifacts/packages`, and removes the worktree. Service CI and local Docker builds use it.
  - Services pin only tagged (non-prerelease) versions. A RepoChecks rule enforces this: a prerelease pin in any service's props fails.
- **Rationale**: This keeps constitution principle II (pinned per service, MinVer from tags), and builds are reproducible from git alone. The feed folder stays out of git.
- **Alternatives considered**:
  - Floating versions (`0.*`): breaks the pinning rule.
  - Committing the `.nupkg` files: binary churn in git.
  - Packing from `HEAD` in service CI: wrong version.
- **Owner action**: pushing the tag is an outward action. It is done in implement, after the owner confirms.
- **Tag discipline (analysis R1)**: the tag goes on the commit that finishes BuildingBlocks for this PR. Any later BuildingBlocks fix in the same PR gets `building-blocks/v0.1.1` and a pin bump. A pushed tag is never moved.

## R2. Shared authentication in BuildingBlocks.AspNetCore

- **Decision**:
  - `AddBuildingBlocksAuthentication(IConfiguration)` binds the section `Authentication` (`Authority`, `Audience`, `RequireHttpsMetadata`). It registers JWT bearer with `MapInboundClaims = false`, so `sub`, `tenant_id` and `user_type` keep their names. It validates issuer, audience, lifetime and signature.
  - The signing keys come from the authority's discovery document through the standard configuration manager. That cache refreshes on schedule, and also when a token arrives with an unknown key id (the default), which covers key rotation.
  - There are two policies:
    - `tenant`: an authenticated user with a tenant and a user type other than `platform`. It is both the default policy and the fallback policy, so unmarked routes and plain `[Authorize]` routes behave the same.
    - `platform`: `user_type=platform` with no tenant.
  - There are two markers:
    - `[AllowAnonymous]` / `.AllowAnonymous()`: the standard ASP.NET marker, so the allow-list is visible in the code.
    - `[PlatformOnly]` / `.RequirePlatform()`: an attribute implementing `IAuthorizeData` with `Policy = "platform"`.
  - A startup check walks the endpoint data source and throws if any endpoint carries both markers (FR-014).
  - The pipeline order is `UseAuthentication` → `UseBuildingBlocks` (the identity middleware) → `UseAuthorization`. `UseBuildingBlocksAuthentication()` applies it in that order.
- **New pin**: `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.15`, matching the other ASP.NET packages.
- **Rationale**: These are the standard ASP.NET mechanisms. The fallback policy makes "protected by default" automatic (US2 scenario 4).
- **Alternatives considered**: A custom middleware gate. Rejected because it duplicates what the authorization pipeline already does.

## R3. 401 or 403 for malformed identities

- **Finding**: The existing `CurrentUser.TryCreate` already rejects a non-platform user without a tenant, and `IdentityMiddleware` answers **401 `invalid-identity`**. The spec said 403 for that case. Also, the existing test `PlatformUserWithTenantKeepsTenant` accepts a platform token that carries a tenant.
- **Decision**:
  - A malformed identity (missing tenant for non-platform, unknown `user_type`, or **platform with a tenant**) is **401 `invalid-identity`**. The token can't describe a valid caller.
  - A well-formed caller of the wrong kind (a platform user on a tenant route, a tenant user on a platform route) is **403**.
  - `CurrentUser.TryCreate` gains the platform-with-tenant rejection, and that test is changed to expect the rejection.
  - The spec is updated to match.
- **Rationale**: 401 means "this credential is not acceptable". 403 means "valid, but not allowed here". This also follows the existing BuildingBlocks behaviour.

## R4. Test tokens

- **Decision**: `BuildingBlocks.Testing` gains:
  - `TestTokens`: a process-wide RSA key, plus `For(userId, tenantId, userType)` and `Platform(userId)`, which create signed JWTs with the configured issuer and audience.
  - `services.AddTestTokenValidation()`: post-configures the JWT bearer options to trust that key, with a static configuration and no discovery call.
- **Rationale**: The integration tests then run the real validation pipeline: signature, audience, lifetime, claims. A fake auth handler would skip it.
- **Package additions to Testing**: `FrameworkReference Microsoft.AspNetCore.App`, and `Microsoft.IdentityModel.JsonWebTokens` (which comes in transitively through JwtBearer, with its version pinned centrally).

## R5. The no-roles route test

- **Decision**: `NoRolesRouteCheck.RunAsync(IServiceProvider services, HttpClient client, TestTokenSet tokens)` returns a report (checked, skipped, failures). xUnit asserts that there are no failures and prints the report. It works like this:
  - Discovery: every `RouteEndpoint` in `EndpointDataSource`, each HTTP method from `HttpMethodMetadata`.
  - Category from metadata: `IAllowAnonymous` → skip; `PlatformOnlyAttribute` → platform token; otherwise tenant token (with a random tenant, and no role assignments).
  - Route parameters: `{x:guid}` → a new GUID, `{x:int}` → `1`, anything else → `x`. Catch-alls get `x`.
  - Body: `{}` as JSON for POST, PUT and PATCH.
  - Expectation: 403.
- **Known risk, recorded for 013**: on a command route, the validation middleware could answer 400 for `{}` before the permission check runs. No command routes exist in 011. 013 must make the permission check run before validation, or the route test must accept that the permission check comes first. This is noted as a carry-over, with no change now.

## R6. The HTTP layer and the handler

- **Decision**:
  - Identity references `WolverineFx.Http`. `GET /roles` is a Wolverine HTTP endpoint that only builds the `GetRoles` query and calls `IMessageBus.InvokeAsync<RolesResponse>`.
  - `GetRolesHandler : CommandHandler<IdentityDbContext>` does `Permissions.EnsureAsync(IdentityPermissions.RolesView)`, builds the response from the catalogue and sets `CanAssign = await Permissions.HasAsync(IdentityPermissions.RolesAssign)`. No role holds `identity.roles.assign` yet, so the flag is false for everyone (FR-019).
  - `PermissionDeniedException` is already mapped to 403 problem details by BuildingBlocks.
- **Verify in implement**: the exact Wolverine HTTP mapping API for "route → message" in WolverineFx.Http 6.65.1. Use the delegating form above if no direct mapper exists.

## R7. Persistence, migrations and health

- **Decision**:
  - `IdentityDbContext : TenantDbContext` (BuildingBlocks EF), with `RoleAssignment` (tenant-owned).
  - Wolverine message storage goes in schema `wolverine` (BuildingBlocks messaging).
  - EF migrations live in `Identity.Api/Persistence/Migrations`.
  - A hosted `MigrationRunner` applies migrations on startup, retrying with backoff while the database is unreachable. Npgsql's migration lock serializes concurrent instances (verify in implement with two instances, US1 edge case).
  - Health:
    - `/health/live`: no checks.
    - `/health/ready`: the database can connect, and the migrations are applied.
    - Neither depends on the token authority.
- **Rationale**: Readiness turns healthy only once migrations are done (FR-007). Liveness stays up while the database is down.

## R8. Wolverine code generation

- **Decision**:
  - In `Program.cs`, `TypeLoadMode` is `Dynamic` under `#if DEBUG` and `Static` otherwise.
  - `WolverineFx.RuntimeCompilation` is referenced only when `'$(Configuration)' == 'Debug'`.
  - Generated code lives in `Identity.Api/Internal/Generated/`, written by `dotnet run --project Identity.Api -c Debug -- codegen write`. That needs `return await app.RunJasperFxCommands(args);`.
  - The CI guard runs `codegen write`, then fails if `git status --porcelain -- src/services/identity/Identity.Api/Internal/Generated` shows anything.
  - Release has no runtime compiler, so a missing type fails at startup, which the smoke start catches (FR-024).
- **Verify in implement**: that `codegen write` runs without the database and broker. If it doesn't, the guard step starts them first.

## R9. Docker build

- **Decision**:
  - Multi-stage `Dockerfile` with `ARG DOTNET_SDK_IMAGE` and `ARG DOTNET_ASPNET_IMAGE` (no defaults).
  - `COPY --from=packages . /repo/artifacts/packages`. The service is copied to `/repo/src/services/identity`, so the service's own `nuget.config` (source `../../../artifacts/packages`, `<clear/>`, the same source mapping) resolves the same path locally and in Docker.
  - `dotnet publish -c Release -p:MinVerSkip=true` (no `.git` in the context).
  - The final image runs as the non-root `$APP_UID` user and listens on 8080.
- **Self-contained build settings**: the service gets its own `Directory.Build.props` (the same settings as the root: nullable, warnings as errors, analysis level) and `Directory.Packages.props`, with no imports from outside its folder. `global.json` is not needed inside Docker, because the SDK image is pinned to the same version.
- **Healthcheck**: `wget -qO- http://localhost:8080/health/ready`. Alpine images include BusyBox wget.

## R10. Compose and the dev settings

- **Decision**:
  - `src/services/identity/compose.yaml`:
    - `identity-db`: `${POSTGRES_IMAGE}`, profile `infra`, port `127.0.0.1:${IDENTITY_DB_PORT}:5432`, volume `identity-db-data`, `pg_isready` healthcheck.
    - `identity-api`: built with `additional_contexts: packages: ../../../artifacts/packages` and args from `versions.env`. Profile `apps`, depends on `identity-db` and `rabbitmq` being healthy, port `127.0.0.1:${IDENTITY_API_PORT}:8080`, temporary until 014.
  - `deploy/dev.env` gains `IDENTITY_DB_USER`, `IDENTITY_DB_PASSWORD` (dev-only) and `IDENTITY_API_PORT=5101`. `deploy/README.md` gets the port row and a note that the port is temporary.
  - `deploy/compose.yaml` gets one include entry.
- **Rationale**: FR-004 and FR-005. `depends_on` across included files works, because they are one project.

## R11. CI

- **Decision**: `.github/workflows/identity.yml` runs on `src/services/identity/**` and the workflow file itself, with these steps:
  1. Checkout with full history and tags, then setup-dotnet.
  2. `restore-feed.ps1 -Service identity`.
  3. Pull the test images.
  4. Build and test the solution (unit, integration, route test).
  5. Codegen guard.
  6. Image-pin check, and `RepoChecks -- packages` (no prerelease pins).
  7. Smoke test: `dev.ps1 up -Profile infra,apps`, which builds the image in Release (Static mode) and waits for healthy.
  8. `if: always()`: `dev.ps1 reset`, then the cleanup check with `-Project inventory-and-order`.
- `building-blocks.yml` gets tests for the new authentication and route-test code. The existing consumer checks cover the packages.

## R12. Constitution v1.3.0

- **Decision**: Run `/speckit-constitution` to make a MINOR change in principle VI. Replace "Every route requires an authenticated user with a tenant, except an explicit, reviewed allow-list" with three rules:
  1. By default a route requires an authenticated user with a tenant.
  2. Platform-only routes are an explicit, reviewed category, open only to `user_type=platform` with no tenant.
  3. The allow-list (no authentication) stays explicit and reviewed.

  Also extend the route-test sentence to cover both categories, and update the version and date.
- **Clarification in the same amendment (analysis D1)**: the sentence "Authorization attributes and policies are not used" becomes: route-category markers and policies (tenant default, platform-only, allow-list) are used, because they check who the caller is, not what they may do. Permissions are still checked only through explicit `IPermissionManager` calls, never through attributes or policies.
