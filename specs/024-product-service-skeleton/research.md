# Research: Product Service Skeleton

Read on 2026-11-27 from the repository:
- Identity's layout, `Program.cs`, Dockerfile, compose, `identity.yml`, `StartupTasks` and the `ListRoles` slice
- BuildingBlocks 0.4.1 `AddBuildingBlocksAuthentication` and `AuthenticationSettings`
- the gateway's `IdentityBackchannelHandler`, `appsettings.json` and compose
- `deploy/compose.yaml`, `deploy/dev.env`, `tests/e2e/compose.e2e.yaml`
- `scripts/restore-feed.ps1` and `scripts/codegen.ps1`

## R1. Validating Identity's tokens from inside Docker

- **Facts**:
  - Tokens carry the issuer `http://localhost:8080/` (`IDENTITY_ISSUER`, the gateway origin).
  - Identity sets that issuer explicitly, so its discovery document lists the issuer and the `jwks_uri` under that origin.
  - Inside the compose network, `localhost:8080` is the Product container itself, not Identity.
  - BuildingBlocks 0.4.1 `AddBuildingBlocksAuthentication` sets only `Authority` and `Audience`; no service used it until now, because Identity validates through OpenIddict locally.
- **Decision**: BuildingBlocks.AspNetCore 0.5.0.
  - It adds `Authentication:InternalAddress`, an optional absolute URI.
  - When set, `JwtBearerOptions.BackchannelHttpHandler` is an `IssuerBackchannelHandler`. It rewrites requests whose scheme, host and port match the `Authority` to the internal address, and sets `X-Forwarded-Proto` and `X-Forwarded-Host` to the authority's.
  - Identity already trusts forwarded headers from the Docker networks (`IDENTITY_FORWARDED_NETWORK_*`), so it answers with public URLs, and issuer validation still checks the public issuer.
  - When `InternalAddress` is not set, nothing changes.
  - Product sets `Authentication__Authority=${IDENTITY_ISSUER}`, `Authentication__Audience=product` and `Authentication__InternalAddress=http://identity-api:8080`.
- **Why**:
  - It is the same proven technique as the gateway's backchannel, in the shared package, so Inventory and Order get it too.
  - Tokens keep one public issuer.
- **Verified by**:
  - BuildingBlocks tests: a fake authority behind an internal address; rewriting, headers, and no rewrite for other hosts
  - the e2e check (R7): a real token through the gateway reaches Product and gets 403, not 401
- **Rejected**:
  - Setting `MetadataAddress` to the internal URL: the `jwks_uri` inside the document is still public.
  - A second issuer for internal calls: it splits validation and weakens the issuer check.
  - Product reaching Identity through the gateway: it adds a hop and a dependency on the gateway's routes for protocol endpoints.
- **Release**:
  - the BuildingBlocks workflow packs 0.5.0
  - the tag `building-blocks/v0.5.0` is pushed with the owner's approval, before Product's CI can restore the feed (`restore-feed.ps1` builds pinned versions from tags)
  - Identity stays on 0.4.1 (versions are pinned per service)

## R2. Service layout

Mirrors Identity without OpenIddict, Identity, Data Protection, Razor pages, lookups or seeds:
- **Projects**: `Product.slnx` with `Product.Api`, `Product.Contracts` and `Product.Tests`.
- **Build settings**: `Directory.Build.props` (warnings as errors, latest-recommended analysis, `MinVerTagPrefix` `product-contracts/v`), `Directory.Packages.props` (BuildingBlocks 0.5.0 and the same third-party versions as Identity), and `nuget.config` (local feed for `BuildingBlocks.*` and `*.Contracts`).
- **`Program.cs`**:
  - `AddBuildingBlocks`, `AddBuildingBlocksPermissions<ProductPermissionProvider>`, `AddDbContextWithWolverineIntegration<ProductDbContext>` and `AddBuildingBlocksAuthentication`
  - health checks, `AddWolverineHttp`, and `UseWolverine` with `UseBuildingBlocksMessaging("product", …)`, `UseBuildingBlocksPermissionInvalidation` and `UseBuildingBlocksValidation`
  - Dynamic code generation in Debug, Static in Release
  - `UseBuildingBlocksAuthentication`, then `/health/live` and `/health/ready` (`AllowAnonymous`), then `MapWolverineEndpoints`
- **Startup**: `StartupTasks` runs the migrations with the same retry as Identity. EF's migration lock makes concurrent starts safe. `MigrationState` gates readiness.
- **Code generation**: `codegen.env` sets fake `Authentication__Authority` and `Audience` values, because `ValidateOnStart` requires them during `codegen write`.
- **Broker at start-up**: compose makes `product-api` wait for a healthy broker, as for Identity. No separate test is added; the compose rule is checked in T006.

## R3. Roles and permissions

- **Permissions**: `product.roles.view` ("View roles"). Later features add `product.products.*`.
- **Roles**:
  - `product.administrator` ("Administrator") has every Product permission
  - `product.viewer` ("Viewer") has `product.roles.view`
  - a test checks every role and permission has a display name, as in Identity
- **`ProductPermissionProvider`**:
  - customers and platform users get no permissions
  - a tenant user gets the permissions of their `role_assignments` rows (`TenantId`, `UserId`, `Role`)
  - 025 adds the local-user disabled check and the assignment endpoints
- **`GET /roles`**: `EnsureAsync(product.roles.view)`, then roles and permissions sorted by display name, as in Identity. `canAssign` is `false`, because no assign permission exists yet.
- **Why**: one route behind a real permission gives the no-roles test something to check, as 011 did.

## R4. Database

- **Tables**: `role_assignments` (`TenantId`, `UserId`, `Role`, `AssignedAt`; key `(TenantId, UserId, Role)`; check constraint on known roles), plus the Wolverine envelope tables created by the messaging setup.
- **Migration**: `Initial`.
- **Settings**: compose `product-db` uses `POSTGRES_DB: product`, `PRODUCT_DB_USER` and `PRODUCT_DB_PASSWORD`, which are added to `deploy/dev.env`.

## R5. Gateway

- **Route and cluster**:
  - in `appsettings.json`, route `api-product` matches `/api/product/{**rest}` with `Session: required` and `PathRemovePrefix /api/product`
  - cluster `product` points at `http://product-api:8080`, with the address also set in the gateway compose like the Identity cluster
- **Tests**: the route-table test now lists `/api/product`. A forwarding test checks the bearer token, the app header and the prefix removal, as the existing `/api/identity` tests do.
- **Start order**: the gateway does not wait for `product-api`. YARP answers 502 or 503 until it is up, which is the behaviour Identity's route has without health gating.

## R6. CI and the dev-stack workflow

- **`product.yml`**: a copy of `identity.yml` for `product`.
  - paths: `src/services/product/**` and the workflow
  - steps: restore the feed for `-Service product`, the tests, `codegen.ps1 -Service product -Check`, the image and package pin checks, the smoke start of `product-api` in Static mode, then the smoke checks
  - smoke checks: ready; `/roles` answers 401 without a token; no published port; the log shows Static mode
  - then reset and the cleanup check
- **`deploy.yml` and `e2e.yml`**: add `src/services/product/**` to their paths. `deploy.yml` also restores the Product feed.
- **`scripts/e2e.ps1`**: today it restores the feed only for Identity (`restore-feed.ps1 -Service identity`). It must also restore it for Product, or the end-to-end build of `product-api` cannot find BuildingBlocks 0.5.0 in CI.
- **Gateway workflow**: runs because the gateway files change.

## R7. End-to-end

- **Compose**: `compose.e2e.yaml` puts `product-db` on tmpfs with no ports or volumes, like `identity-db`.
- **New spec**: `product-service.spec.ts` signs in as the demo admin and calls `/api/product/roles` through the page's request context with the app header, then expects 403 with `missingPermissions` listing `product.roles.view`.
- **What it proves**: the gateway route works, and Product validates a real Identity token inside Docker (R1).

## R8. No-roles route test

`NoRolesRouteTests` follows Identity's test and uses the BuildingBlocks route check on `ProductApp`. It covers `GET /roles`; health is allow-listed.

## R9. Findings during implementation (2026-11-27)

- **The internal address works end to end**: the e2e check gets 403 from Product with `missingPermissions: ["product.roles.view"]`, so a real Identity token was validated inside Docker. Identity's discovery document lists `issuer` and `jwks_uri` under `http://localhost:8080/`, checked on the dev stack before building R1.
- **Packing 0.5.0**:
  - MinVer reads the version from tags, so the BuildingBlocks change was committed on this branch and tagged `building-blocks/v0.5.0` locally, as 0.4.1 was in 019
  - the tag is pushed only with the owner's approval
- **`pack.ps1` packs every service's Contracts project** (it packed `Identity.Contracts` as a prerelease), so `Product.Contracts` will be packed too once it carries messages (026).
- **Permissions are needed for code generation**:
  - `UseBuildingBlocksPermissionInvalidation` brings a handler that needs `IPermissionCache`
  - so `AddBuildingBlocksPermissions<ProductPermissionProvider>` had to exist before the first `codegen write`
  - the provider moved from T007 into T005's work; T007's tests cover it
- **Readiness waits for Wolverine**: `StartupCompletion`, registered after `UseWolverine`, marks the service ready, so the outbox and inbox tables exist before readiness turns healthy (FR-007, as in Identity).
- **`TenantId` is 64 characters**: `TenantDbContext` sets the length for every tenant-owned entity, so the 32 in the first data model was wrong and is removed.
- **`.dockerignore` is required**: without it, local `bin` and `obj` folders reach the build context and the Release publish fails. It is copied from Identity.
- **`codegen.env` only sets the environment**: `codegen.ps1` already supplies placeholder `Authentication__Authority` and `Audience`.
- **The 403 body lists `missingPermissions`**, not `missing`; the docs are corrected.
- **The first start on a fresh database logs one failed command**: EF reading `__EFMigrationsHistory` before it exists. EF does this on every first migration; it is harmless.
- **Time to ready**: 42 seconds for the whole stack on a warm cache (SC-001).
- **Tests that passed when written**: the 401 and 403 authentication tests (T008). The token checks come from BuildingBlocks, which the T001 tests already covered.
