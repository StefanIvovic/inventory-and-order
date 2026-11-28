# Tasks: Product Service Skeleton

**Input**: Design documents from `specs/024-product-service-skeleton/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/http.md, contracts/configuration.md, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**:
- No comments.
- Never reset the dev stack (CI's own reset is fine).
- Mirror Identity's files unless research.md says otherwise.
- Product paths are relative to `src/services/product/`.

---

## Phase 1: Foundational (BuildingBlocks 0.5.0)

- [X] T001 Internal address for token metadata, per research.md R1, implementing data-model.md `AuthenticationSettings.InternalAddress`.

  Tests first in `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Authentication/`:
  - with `InternalAddress` set, discovery and key requests for the authority's origin go to the internal address, with `X-Forwarded-Proto` and `X-Forwarded-Host` set to the authority's scheme and host
  - requests to any other host are not rewritten
  - without `InternalAddress`, nothing changes
  - a relative or path-bearing `InternalAddress` fails at start
  - a token signed by a fake authority reachable only at the internal address validates end to end
  - after the fake authority switches to a new signing key, a token signed with it validates without a restart (keys are fetched again through the internal address)

  See them fail, then add `InternalAddress` to `AuthenticationSettings.cs`, a new `IssuerBackchannelHandler.cs`, and the wiring in `AuthenticationExtensions.cs`
- [X] T002 Run `dotnet test src/building-blocks/BuildingBlocks.slnx` and `./scripts/pack.ps1`, so `artifacts/packages` holds 0.5.0 locally. The tag is pushed in T015

---

## Phase 2: User Story 1 - Run the Product service in the dev stack (P1) 🎯 MVP

**Independent Test**: spec.md US1.

- [X] T003 [US1] Service scaffolding, per research.md R2 and plan.md:
  - `Product.slnx`, `Directory.Build.props` (MinVer prefix `product-contracts/v`), `Directory.Packages.props` (BuildingBlocks 0.5.0, the other versions as Identity's), `nuget.config`
  - `Product.Contracts/Product.Contracts.csproj` (packable, no messages)
  - `Product.Api/Product.Api.csproj`, with RuntimeCompilation in Debug only
  - `Product.Tests/Product.Tests.csproj`

  `dotnet build` passes
- [X] T004 [US1] Write the tests first in `Product.Tests/` (`ProductAppFixture.cs` and `ContainersFixture.cs` as Identity's, with test token validation):
  - `HealthTests.cs`: live is 200; ready is 503 before migrations and 200 after; ready is 503 when the database is unreachable
  - two app instances on one fresh database both become ready (concurrent migrations)
  - after start, the Wolverine envelope tables exist in the `product` database (FR-007)
  - `RoleTableTests.cs`: implements data-model.md `RoleAssignment.TenantId`, `.UserId`, `.Role` and `.AssignedAt`; the key, the tenant filter, and the check constraint refusing an unknown role
  - `ContractsPackageTests.cs`: the contracts project is packable and references no other project

  See them fail
- [X] T005 [US1] Implement:
  - `Product.Api/Program.cs` (R2)
  - `Persistence/ProductDbContext.cs`, `RoleAssignment.cs`, `StartupTasks.cs` (migrations with retry, `MigrationState`) and `DesignTimeDbContextFactory.cs`
  - `Health/ReadinessCheck.cs`
  - the `Initial` migration via `dotnet ef migrations add`

  T004 green
- [X] T006 [US1] Docker and compose, per contracts/configuration.md:
  - `Dockerfile`, copied from Identity's, without `/keys`
  - `compose.yaml`: `product-db` in infra (loopback `PRODUCT_DB_PORT`, named volume, healthcheck) and `product-api` in apps (settings, waits for healthy db and rabbitmq, healthcheck, no ports)
  - add `PRODUCT_DB_USER` and `PRODUCT_DB_PASSWORD` to `deploy/dev.env`
  - include `src/services/product/compose.yaml` in `deploy/compose.yaml`
  - put `product-db` on tmpfs in `tests/e2e/compose.e2e.yaml`

  `dev.ps1 up -Profile infra,apps` (never reset): `product-api` is healthy

---

## Phase 3: User Story 3 - Product's roles are defined and enforced (P2)

**Independent Test**: spec.md US3.

- [X] T007 [US3] Tests first:
  - `Product.Tests/RolesTests.cs`:
    - 403 without the permission, listing `product.roles.view`
    - with a `product.viewer` assignment inserted directly: 200 with both roles and their permissions sorted by display name, and `canAssign` false
    - every role and permission has a display name
  - `NoRolesRouteTests.cs` (R8)

  See them fail, then implement `Authorization/ProductPermissions.cs`, `ProductRoles.cs` and `ProductPermissionProvider.cs`, and `Features/ListRoles/GetRoles.cs`, `GetRolesHandler.cs` and `RolesEndpoint.cs`, per research.md R3, data-model.md `ProductPermissions`, `ProductRoles` and the GetRoles answer, and contracts/http.md

---

## Phase 4: User Story 2 - Reach the service only through the gateway, signed in (P1)

**Independent Test**: spec.md US2.

- [X] T008 [US2] `Product.Tests/AuthenticationTests.cs` first:
  - 401 with no token, an expired token, a token for another audience, a wrongly signed token, and an identity-malformed token (`invalid-identity`)
  - 403 for a platform user on `GET /roles`

  Runs after T007 (it needs `GET /roles`). The authentication itself comes from BuildingBlocks, so these may pass when written; if one fails, fix the configuration in `Program.cs`
- [X] T009 [US2] Gateway, per research.md R5. Tests first in `src/gateway/Gateway.Tests/`:
  - `RoutingTests.cs`: the route table lists `/api/product/**` as session-required
  - `ApiForwardingTests.cs`: `/api/product/x` is forwarded to the product cluster without the prefix, with the bearer token; without a session it gets 401

  See them fail, then change `src/gateway/Gateway.Api/appsettings.json` and `src/gateway/compose.yaml`
- [X] T010 [US2] e2e, per research.md R7: `tests/e2e/specs/product-service.spec.ts`. Signed in as the demo admin, `GET /api/product/roles` with `X-Bff-App: backoffice` answers 403, and `missingPermissions` lists `product.roles.view`

---

## Phase 5: User Story 4 - Ship the service safely (P2)

**Independent Test**: spec.md US4.

- [X] T011 [US4] Code generation: add `codegen.env` (contracts/configuration.md), run `./scripts/codegen.ps1 product`, commit `Product.Api/Internal/Generated/`, and check `-Check` passes. Release build and the Static-mode start (`dev.ps1 up -Profile infra,apps -Service product-api`): the log shows Static mode
- [X] T012 [US4] `.github/workflows/product.yml` per research.md R6. Then:
  - add `src/services/product/**` to `deploy.yml` and `e2e.yml`
  - add `restore-feed.ps1 -Service product` to `deploy.yml`
  - make `scripts/e2e.ps1` restore the feed for `product` as well as `identity`
  - run `tools/RepoChecks images` and `packages` locally

---

## Phase 6: Polish

- [X] T013 Run `dotnet test` for BuildingBlocks, Product, Identity and the gateway, and `scripts/check-analyzers.ps1`. Check new files for comments. Check whether `scripts/pack.ps1` or the BuildingBlocks workflow packs `Product.Contracts`, and record the answer in R9 (packing matters from 026)
- [X] T014 Rebuild with `dev.ps1 up -Profile infra,apps` (never reset). Check quickstart.md section 4, and that `product-api` is ready within 60 seconds (SC-001). Then `scripts/e2e.ps1`: all specs pass, and the cleanup check is clean
- [X] T015 Record findings in research.md (R9). Commit in chunks: docs; BuildingBlocks; Product service; gateway; deploy, e2e and workflows. After the owner confirms, push the tag `building-blocks/v0.5.0` first, so the PR's CI can restore the feed; then push the branch, open the PR and watch CI

---

## Dependencies

- T001 → T002 → everything else (Product needs the 0.5.0 packages).
- T003 → T004 → T005 → T006.
- US3 (T007) needs T005. It comes before US2, which calls `GET /roles`.
- US2: T008 needs T007; T009 needs nothing in Product; T010 needs T006, T007 and T009.
- US4 (T011–T012) needs T007, so the generated code includes the roles handler.
- Polish comes last.

## Parallel Examples

- T009 (gateway) alongside T007 or T008 (Product), since they touch different folders.
- T012 (workflows) alongside T011.

## Implementation Strategy

MVP is BuildingBlocks 0.5.0 plus US1: the service runs in the dev stack. US2 and US3 make it reachable and protected; US4 makes it shippable. One PR.
