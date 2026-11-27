# Implementation Plan: Product Service Skeleton

**Branch**: `024-product-service-skeleton` | **Date**: 2026-11-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/024-product-service-skeleton/spec.md`

## Summary

A second service, Product, built like Identity's 011 skeleton:
- **What it has**: its own folder, database, compose entry, CI workflow and committed generated code; a no-roles route test; and `GET /roles` over Product's roles from code.
- **Reached through**: the gateway at `/api/product/**`.

One BuildingBlocks change is needed (R1):
- **Why**: Product is the first service that validates Identity's tokens from outside Identity. Inside Docker, the discovery document and the signing keys live at the public issuer (`http://localhost:8080/`), which the container cannot reach.
- **Change**: BuildingBlocks.AspNetCore 0.5.0 adds an optional `Authentication:InternalAddress`. When it is set, metadata and key requests to the issuer's origin go to that address with the forwarded headers Identity expects. The gateway's `IdentityBackchannelHandler` already does the same.
- **Versions**: Product pins 0.5.0. Identity stays on 0.4.1.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**:
- BuildingBlocks 0.5.0: AspNetCore, EntityFrameworkCore, Messaging, Validation, Testing
- WolverineFx.Http and RuntimeCompilation (Debug only), Npgsql EF Core, EF Core Design
- No OpenIddict in Product

**Storage**: PostgreSQL `product` database: role assignments, Wolverine outbox and inbox, EF migrations history

**Testing**:
- xUnit v3 and Testcontainers for Postgres and RabbitMQ
- BuildingBlocks test tokens (`AddTestTokenValidation`)
- the no-roles route test
- an e2e check through the gateway

**Target Platform**: Linux container (aspnet image from `deploy/versions.env`)

**Project Type**: web service (new), plus a BuildingBlocks package change and a gateway configuration change

**Performance Goals**: ready about as fast as Identity; no request-path work besides the permission lookup

**Constraints**:
- constitution I, II, VI, VII, VIII and IX
- the build context is the service folder only
- packages come only from the local feed
- the generated code is committed

**Scale/Scope**:
- about 25 new files in `src/services/product/`
- 1 BuildingBlocks file and its tests
- gateway: `appsettings.json`, `compose.yaml`, tests
- `deploy/compose.yaml`, `deploy/dev.env`, the e2e compose and specs
- 3 workflows

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Own folder, database, compose, CI; the Docker context is the folder; packages only from the feed; no project reference outside it |
| II. Shared code via packages | ✅ | The BuildingBlocks change is additive (MINOR, 0.5.0), released by tag through its own workflow; `Product.Contracts` is packable with its own MinVer prefix and no messages yet |
| III. Handlers hold logic | ✅ | `RolesEndpoint` maps to `GetRolesHandler : CommandHandler<ProductDbContext>` |
| IV. Transactions and messaging | ✅ | Wolverine with EF integration, outbox and inbox from `UseBuildingBlocksMessaging`; no events published yet |
| V. Multi-tenancy | ✅ | `RoleAssignment` is `ITenantOwned` behind the global filter; tokens carry `tenant_id` |
| VI. Authorization | ✅ | Roles and permissions in code; `IPermissionManager.EnsureAsync`; the default tenant policy; the no-roles 403 test as a merge gate; `canAssign` is the screen flag |
| VII. Tests isolated | ✅ | Testcontainers; e2e in `e2e-test` with tmpfs for `product-db`; the cleanup check |
| VIII. Docker | ✅ | Images only from `deploy/versions.env` build args; Postgres pinned through `POSTGRES_IMAGE` |
| IX, X | ✅ | No comments (the generated migrations keep their usual `<inheritdoc/>`, as in Identity); no attribution |
| Wolverine code generation | ✅ | Dynamic in Debug, Static in Release; the CI guard and the smoke start |

**Gate: PASS.** **Post-design re-check**: PASS.

## Project Structure

### Documentation

```text
specs/024-product-service-skeleton/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/http.md, contracts/configuration.md
└── tasks.md
```

### Source Code

```text
src/building-blocks/BuildingBlocks.AspNetCore/Authentication/
├── AuthenticationSettings.cs          + InternalAddress (optional)
├── AuthenticationExtensions.cs        backchannel handler when InternalAddress is set
└── IssuerBackchannelHandler.cs        new
src/building-blocks/BuildingBlocks.AspNetCore.Tests/Authentication/   backchannel tests

src/services/product/
├── Product.slnx, Directory.Build.props, Directory.Packages.props, nuget.config, codegen.env
├── Dockerfile, compose.yaml
├── Product.Contracts/Product.Contracts.csproj          packable, MinVer prefix product-contracts/v
├── Product.Api/
│   ├── Program.cs
│   ├── Authorization/ProductPermissions.cs, ProductRoles.cs, ProductPermissionProvider.cs
│   ├── Features/ListRoles/GetRoles.cs, GetRolesHandler.cs, RolesEndpoint.cs
│   ├── Health/ReadinessCheck.cs
│   ├── Persistence/ProductDbContext.cs, RoleAssignment.cs, StartupTasks.cs, DesignTimeDbContextFactory.cs, Migrations/
│   └── Internal/Generated/                             committed Static code
└── Product.Tests/
    ├── ProductAppFixture.cs, ContainersFixture.cs (as Identity's)
    ├── HealthTests.cs, AuthenticationTests.cs, RolesTests.cs, NoRolesRouteTests.cs, RoleTableTests.cs
    └── ContractsPackageTests.cs

src/gateway/Gateway.Api/appsettings.json   route api-product, cluster product
src/gateway/compose.yaml                   product cluster address
src/gateway/Gateway.Tests/                 route-table and forwarding tests for /api/product
deploy/compose.yaml, deploy/dev.env        include product; PRODUCT_DB_USER/PASSWORD
tests/e2e/compose.e2e.yaml                 product-db on tmpfs
tests/e2e/specs/product-service.spec.ts    /api/product/roles → 403 for a signed-in staff user
.github/workflows/product.yml (new), building-blocks.yml (unchanged), deploy.yml and e2e.yml (product paths)
```

**Structure Decision**: Product mirrors Identity's layout, minus OpenIddict, Razor pages, lookups and seeds.

## Complexity Tracking

No deviations. The BuildingBlocks change is a required, additive dependency of this feature (R1), not a deviation.
