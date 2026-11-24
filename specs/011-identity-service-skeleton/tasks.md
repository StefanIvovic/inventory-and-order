---

description: "Task list for feature 011 Identity Service Skeleton"
---

# Tasks: Identity Service Skeleton

**Input**: Design documents from `specs/011-identity-service-skeleton/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. The spec requires the route test as a merge gate, plus integration tests for auth and `/roles`.

**Global rules**:
- No comments.
- Pinned versions.
- No project references outside the service folder.
- Tests use Testcontainers only.
- Every task leaves the build green.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Pin `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15 in `src/building-blocks/Directory.Packages.props`. Reference it from `BuildingBlocks.AspNetCore.csproj`. Add `FrameworkReference Microsoft.AspNetCore.App` and a reference to `BuildingBlocks.AspNetCore` to `BuildingBlocks.Testing.csproj` (research.md R2, R4)

---

## Phase 2: Foundational (BuildingBlocks 0.1.0; blocks every story)

- [X] T002 In `src/building-blocks/BuildingBlocks.Core/Identity/CurrentUser.cs`, reject `user_type=platform` with a `tenant_id` claim. Change `PlatformUserWithTenantKeepsTenant` in `BuildingBlocks.Core.Tests/Identity` to expect the rejection (research.md R3)
- [X] T003 Create `src/building-blocks/BuildingBlocks.AspNetCore/Authentication/` per contracts/http-and-packages.md:
  - `AuthenticationSettings`, bound from `Authentication` and validated on start: `Authority` and `Audience` required
  - `AuthorizationPolicies` (`tenant`, `platform`), with requirements reading `CurrentUser` (data-model.md "Route category")
  - `PlatformOnlyAttribute : IAuthorizeData`, and `RequirePlatform()`
  - `AddBuildingBlocksAuthentication`: JwtBearer with `MapInboundClaims=false`, issuer, audience, lifetime and signature validated; `tenant` set as both the default and the fallback policy
  - `UseBuildingBlocksAuthentication`, in the order authentication → identity middleware → authorization
  - a startup check that throws when an endpoint carries both `IAllowAnonymous` and `PlatformOnly` (research.md R2)
- [X] T004 [P] Add `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Authentication/` tests on a test app with test tokens:
  - 401 for: no token, expired, unknown key, wrong audience
  - 401 `invalid-identity` for: platform with tenant, staff without tenant
  - 403 for: a platform user on an unmarked route, a staff user on a platform route
  - 200 for: the right caller on each kind of route, and anyone on an `AllowAnonymous` route
  - startup fails when an endpoint has both markers
  - with the authority unreachable (no test key override, discovery URL on a closed port), the app starts, liveness answers 200, and token-protected routes answer 401
  - key rotation: after the signing-key set changes to include a second key, a token signed with that key is accepted without a restart (analysis C1)
  - the two tests above use a stub authority instead of `AddTestTokenValidation`: a small in-process server (Kestrel on a random loopback port) serving `/.well-known/openid-configuration` and a JWKS document whose key set the test can change, so the real discovery and refresh path is exercised (analysis U1)
- [X] T005 Create `src/building-blocks/BuildingBlocks.Testing/Tokens/TestTokens.cs` and `TestTokenValidation.cs` per the contract: a process-wide RSA key, `For`, `Platform`, `Raw`, and `AddTestTokenValidation`, which post-configures JwtBearer with a static configuration (research.md R4)
- [X] T006 Create `src/building-blocks/BuildingBlocks.Testing/Routes/NoRolesRouteCheck.cs` and `RouteCheckReport.cs`:
  - discovery from `EndpointDataSource`, every method of each route
  - category from metadata
  - placeholder values for route parameters
  - `{}` as the body for POST, PUT and PATCH
  - expect 403; report checked, skipped and failures (research.md R5)
- [X] T007 [P] Add `BuildingBlocks.Testing.Tests` tests for the route check against a small test app:
  - a guarded route passes
  - an unguarded route is reported as a failure, with its route and status
  - an `AllowAnonymous` route is skipped
  - a `{id:guid}` route and a POST route are called
  - a platform route is called with a platform token
- [X] T008 Run `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` and `./scripts/pack.ps1`, and run the consumer checks for AspNetCore and Testing. All must pass

**Checkpoint**: BuildingBlocks is ready to release.

---

## Phase 3: Release plumbing

- [X] T009 Create `scripts/restore-feed.ps1 -Service <name>`:
  - reads the `BuildingBlocks.*` versions from `src/services/<name>/Directory.Packages.props`
  - fails if they differ from each other, if the version is a prerelease, or if the tag `building-blocks/v<version>` is missing
  - packs that tag from a temporary `git worktree` with `scripts/pack.ps1 -Output <repo>/artifacts/packages`, and always removes the worktree (research.md R1)
- [X] T010 [P] Add a RepoChecks command `packages` (`dotnet run --project tools/RepoChecks -- packages`) plus tests in `tools/RepoChecks/` and `tools/RepoChecks.Tests/`: a `BuildingBlocks.*` or `*.Contracts` pin in `src/services/*/Directory.Packages.props` that is a prerelease fails (research.md R1)
- [ ] T011 Commit the BuildingBlocks work. After the owner confirms, create and push the tag `building-blocks/v0.1.0` on that commit. Never move it: any later BuildingBlocks fix gets `v0.1.1` and a pin bump (research.md R1, analysis R1). Run `./scripts/restore-feed.ps1` once against a scratch props file to prove it packs `0.1.0`

---

## Phase 4: User Story 6 - The service builds and ships on its own (P2, the base for the other stories)

- [X] T012 [US6] Create `src/services/identity/` with:
  - `Identity.slnx`
  - `Directory.Build.props`: the same settings as the root, no imports
  - `Directory.Packages.props`: BuildingBlocks `0.1.0`, Wolverine 6.65.1, EF and Npgsql, JwtBearer, xunit.v3, Testcontainers, `Microsoft.AspNetCore.Mvc.Testing` 10.0.15; MinVer as a global reference with `MinVerTagPrefix identity/v`
  - `nuget.config`: `<clear/>`, source `../../../artifacts/packages`, the same source mapping as the root
  - `Identity.Api` (Web SDK)
  - `Identity.Contracts` (packable, empty)
  - `Identity.Tests` (xunit.v3, references Identity.Api)

  Add the projects to `InventoryAndOrder.slnx` (plan.md, research.md R9)
- [X] T013 [US6] Create `src/services/identity/Dockerfile` per research.md R9: build args with no defaults, `COPY --from=packages`, publish in Release with `MinVerSkip`, non-root final stage on 8080
- [X] T014 [US6] Create `src/services/identity/compose.yaml` per research.md R10. Give `identity-api` the dev environment `Authentication__Authority=http://identity-api:8080`, `Authentication__Audience=identity` and `Authentication__RequireHttpsMetadata=false`, plus its connection string and RabbitMQ URI from `dev.env` values (analysis U1). Add `IDENTITY_DB_USER`, `IDENTITY_DB_PASSWORD` and `IDENTITY_API_PORT=5101` to `deploy/dev.env`, the include entry to `deploy/compose.yaml`, and the ports row plus the temporary-port note to `deploy/README.md`

**Checkpoint**: `docker compose -f deploy/compose.yaml --profile '*' config --quiet` passes, and so does the image-pin check.

---

## Phase 5: User Story 1 - Run Identity in the dev stack (P1) 🎯 MVP

**Independent Test**: quickstart.md sections 2 and 3.

- [X] T015 [US1] Create `Identity.Api/Persistence/`:
  - `IdentityDbContext : TenantDbContext`, with `RoleAssignment` per data-model.md "RoleAssignment" (all fields, key and index)
  - the initial migration in `Persistence/Migrations`, including a check constraint that `Role` is one of the `IdentityRoles` names, per data-model.md "RoleAssignment.Role" (analysis C4)
  - `MigrationRunner`, a hosted service with retry and backoff that sets a ready flag (research.md R7)
- [X] T021 [US1] Create `Identity.Api/Authorization/IdentityPermissions.cs` and `IdentityRoles.cs` per data-model.md "IdentityPermissions" and "IdentityRoles". Create `IdentityPermissionProvider.cs` per data-model.md "Permission resolution" It is moved here because T016 registers the provider (analysis F1)
- [X] T016 [US1] Create `Identity.Api/Program.cs`:
  - `AddBuildingBlocks`, `AddBuildingBlocksAuthentication`, `AddBuildingBlocksPermissions<IdentityPermissionProvider>`
  - Wolverine with `UseBuildingBlocksMessaging(new MessagingSettings("identity", …))`. Do not add `UseBuildingBlocksValidation` yet: nothing in 011 needs it, and it comes with the first command route in 013 (analysis S1)
  - `TypeLoadMode` Dynamic under DEBUG and Static otherwise (research.md R8)
  - `MapWolverineEndpoints`
  - health endpoints `/health/live` (no checks) and `/health/ready` (`ReadinessCheck`: the database connects and the migrations are done), both `.AllowAnonymous()`
  - `return await app.RunJasperFxCommands(args)`

  Configuration comes from `ConnectionStrings:Identity`, `RabbitMq:Uri` and `Authentication:*`. Reference `WolverineFx.RuntimeCompilation` only in Debug
- [X] T017 [US1] Create `Identity.Tests/IdentityAppFixture.cs`: `PostgresFixture` (a database per test class) and `RabbitMqFixture`, then a WebApplicationFactory that overrides the connection strings and calls `AddTestTokenValidation("identity")`
- [X] T018 [P] [US1] Add `Identity.Tests/HealthTests.cs`: live and ready answer 200 without a token, the migrations are applied on startup, and ready answers 503 while the runner hasn't finished (simulated with an unreachable database)
- [X] T019 [US1] Run quickstart.md sections 2 and 3 locally against the dev stack, and record how long `up` takes against SC-001. Then check concurrent migrations: run a second `identity-api` instance against an empty `identity-db` at the same time as the first (`docker compose ... up --scale identity-api=2` with the dev port removed in a scratch override), and confirm the migration is applied once and both instances become healthy. Then `dev.ps1 reset` (analysis C2)

---

## Phase 6: User Story 2 - Protected by default (P1)

**Independent Test**: `Identity.Tests/AuthenticationTests.cs`.

- [X] T020 [US2] Add `Identity.Tests/AuthenticationTests.cs` for `GET /roles`:
  - no token, invalid signature, expired, wrong audience → 401
  - platform with tenant, staff without tenant → 401 `invalid-identity`
  - a valid platform token → 403
  - a valid staff token with no roles → 403

---

## Phase 7: User Story 4 - The role catalogue (P2)

**Independent Test**: `Identity.Tests/RolesTests.cs`.

- [X] T022 [US4] Create `Identity.Api/Roles/GetRoles.cs` (`GetRoles`, `RolesResponse`, `RoleDto` per data-model.md "GetRoles → RolesResponse"), `GetRolesHandler.cs` (`CommandHandler<IdentityDbContext>`, `EnsureAsync(RolesView)`, `CanAssign = HasAsync(RolesAssign)`) and `RolesEndpoint.cs` (Wolverine HTTP `GET /roles` delegating to `InvokeAsync`) (research.md R6)
- [X] T023 [P] [US4] Add `Identity.Tests/RolesTests.cs`:
  - a staff user assigned `identity.administrator` in their tenant gets 200 with the full catalogue and `canAssign=false`
  - an assignment in another tenant does not grant access (403 problem details listing `identity.roles.view`)
- [X] T024 [P] [US4] Add `Identity.Tests/CatalogueTests.cs`: every role's permissions exist in `IdentityPermissions`, and the names are unique, lowercase and dot-separated

---

## Phase 8: User Story 3 - Route test as a merge gate (P1)

- [X] T025 [US3] Add `Identity.Tests/NoRolesRouteTests.cs`. It runs `NoRolesRouteCheck.RunAsync` on the app, asserts no failures, asserts that `GET /roles` was checked and both health routes were skipped, and writes the report to the test output
- [X] T026 [US3] Mutation check (local only, not committed): add a scratch route that returns 200 without a permission check, confirm the route test fails and names it, then remove it

---

## Phase 9: User Story 5 - Static code in production (P2)

- [X] T027 [US5] Run `dotnet run --project src/services/identity/Identity.Api -c Debug -- codegen write` and commit `Identity.Api/Internal/Generated/`. If the command needs the database or broker, start `dev.ps1 up` first and document that in quickstart.md (research.md R8)
- [X] T028 [US5] Mutation check (local, not committed): change the `GetRoles` handler signature without regenerating, and confirm `git status --porcelain` on the generated folder shows a difference after `codegen write`. Revert
- [X] T029 [US5] Build the image and start `dev.ps1 up -Profile infra,apps`. Confirm `identity-api` is healthy, check the logs for no runtime compilation, and confirm `/roles` answers 401. Then `dev.ps1 reset`

---

## Phase 10: CI and the constitution

- [ ] T030 [US6] Create `.github/workflows/identity.yml` per research.md R11. It triggers on `src/services/identity/**` and the workflow file, with steps:
  1. full-history checkout, setup-dotnet
  2. `restore-feed.ps1`
  3. pull the test images
  4. `dotnet test --solution src/services/identity/Identity.slnx`
  5. the codegen guard
  6. the image-pin check and `RepoChecks -- packages`
  7. the smoke start through `dev.ps1 up -Profile infra,apps`
  8. `if: always()`: `reset`, then the cleanup check `-Project inventory-and-order`
- [X] T031 [US7] Run `/speckit-constitution` to amend to v1.3.0 per research.md R12, including the clarification about route-category markers and policies (analysis D1)

---

## Phase 11: Polish

- [X] T032 Run the full quickstart.md and both solutions' tests. Count the setup lines Identity needs for the shared authentication and the route test, against SC-006 (about 10 each) (analysis C3). Confirm no comments were added, then `dev.ps1 reset` and `check-test-cleanup.ps1`
- [ ] T033 Commit, push, open the PR, and confirm that `building-blocks.yml`, `deploy.yml` and `identity.yml` pass

---

## Dependencies

- T001 → T002–T008 → T009–T011 (tag) → T012–T014 → T015–T017 → stories US1, US2, US4, US3, US5 → T030–T033
- T015 → T021 → T016 (Program.cs registers the provider). T025 needs T022 (at least one guarded route)
- T010 adds `RepoChecks -- packages`, which T030 runs. A step running it is also added to `.github/workflows/building-blocks.yml` (analysis U2)
- T031 is independent and can run any time

## Parallel Opportunities

- T004 ∥ T007 (different test projects)
- T010 ∥ T009
- T018 ∥ T020, and T023 ∥ T024

## Implementation Strategy

- MVP: Phases 1–5 (BuildingBlocks 0.1.0 released, Identity runs healthy in the dev stack).
- Then the security stories US2 and US3, the catalogue US4, Static code generation US5, and CI with the constitution.
- One PR. The tag push in T011 needs the owner's confirmation.
