---

description: "Task list for feature 004 BuildingBlocks Permissions"
---

# Tasks: BuildingBlocks Permissions

**Input**: Design documents from `specs/004-building-blocks-permissions/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. SC-004 requires every scenario to be automated.

**Global rules**: No comments. Pinned versions. No Docker. No real waiting in tests (FR-008).

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Add `Microsoft.Extensions.TimeProvider.Testing` 10.11.1 to `src/building-blocks/Directory.Packages.props`, and reference it from `BuildingBlocks.AspNetCore.Tests.csproj` (research.md)

---

## Phase 2: Foundational

- [X] T002 [P] Create the Core abstractions in `src/building-blocks/BuildingBlocks.Core/Permissions/`: `IPermissionManager.cs`, `IPermissionProvider.cs` and `IPermissionCache.cs` (contracts/permissions-api.md)
- [X] T003 [P] Create `src/building-blocks/BuildingBlocks.Core/Permissions/PermissionDeniedException.cs`: `Missing` is deduplicated and keeps the requested order, an empty list throws `ArgumentException`, and `Code` is `permission-denied` (data-model.md PermissionDeniedException `Missing`, `Code`)
- [X] T004 [P] Write `src/building-blocks/BuildingBlocks.Core.Tests/Permissions/PermissionDeniedExceptionTests.cs` covering the T003 rules
- [X] T005 Create `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Permissions/FakePermissionProvider.cs`, a singleton that maps a user id to a granted set and counts calls, with an optional "throw next" switch. Extend `TestApp.StartAsync` with an optional `Action<IServiceCollection>` hook. Register `AddBuildingBlocksPermissions<FakePermissionProvider>()`, a `FakeTimeProvider`, and endpoints that call `Ensure`, `EnsureAll`, `Has*` and `GetGranted` and return the results

---

## Phase 3: User Story 1 - Explicit permission checks (Priority: P1) 🎯 MVP

**Goal**: Has, HasAll, HasAny, Ensure, EnsureAll and GetGranted, backed by the service provider

**Independent Test**: `PermissionManagerTests`

- [X] T006 [P] [US1] Write `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Permissions/PermissionManagerTests.cs` for spec US1 scenarios 1–6:
  - has, all and any answers
  - `Ensure` reports the single missing permission
  - `EnsureAll` reports exactly the missing ones, in order
  - `GetGranted` returns the full set
  - an unauthenticated caller gets an empty set, and the provider call count stays 0
  - an empty `EnsureAll` succeeds
  - names are compared ordinally (case matters)
- [X] T007 [US1] Implement `src/building-blocks/BuildingBlocks.AspNetCore/Permissions/PermissionManager.cs` (research.md R3, data-model.md "Permission resolution per request")
- [X] T008 [US1] Implement `src/building-blocks/BuildingBlocks.AspNetCore/Permissions/PermissionOptions.cs` (data-model.md PermissionOptions `CacheDuration`, with startup validation) and `src/building-blocks/BuildingBlocks.AspNetCore/PermissionsExtensions.cs` (research.md R5). Then make T006 pass. A minimal pass-through cache is acceptable until US3

**Checkpoint**: Handlers can check and demand permissions

---

## Phase 4: User Story 2 - Denied checks become a 403 (Priority: P1)

**Goal**: A standard 403 body with `missingPermissions`, and a warning log

**Independent Test**: `PermissionDeniedMappingTests`

- [X] T009 [P] [US2] Write `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Permissions/PermissionDeniedMappingTests.cs`:
  - a demanded missing `orders.cancel` gives 403, `application/problem+json`, `code` `permission-denied`, `missingPermissions` `["orders.cancel"]` and a `traceId`
  - a multi-missing case keeps the order
  - a Warning log entry contains the user id, the missing names and the trace id
  - no Error-level log entry
- [X] T010 [US2] Add the `PermissionDeniedException` case to `src/building-blocks/BuildingBlocks.AspNetCore/Errors/BuildingBlocksExceptionHandler.cs` per research.md R4 and contracts/permissions-api.md "403 body". Then make T009 pass

---

## Phase 5: User Story 3 - Cache with immediate invalidation (Priority: P1)

**Goal**: One provider call per (tenant, user) within the duration, at most one lookup per request, and instant invalidation

**Independent Test**: `PermissionCacheTests`

- [X] T011 [P] [US3] Write `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Permissions/PermissionCacheTests.cs` for spec US3 scenarios 1–5 and the edge cases, using `FakeTimeProvider.Advance`:
  - several checks in one request cause 1 provider call
  - two requests cause 1 call
  - after invalidation the next request causes 1 more call and sees the new set
  - after 60 seconds the next request causes 1 more call. At 59 seconds it is still cached
  - the same user in two tenants, and two users in one tenant, each cause separate calls
  - a provider failure gives 500 and is not cached, so the next request calls the provider again
  - invalidating an unknown key does nothing
  - a platform user without a tenant is cached under an empty tenant segment

  (SC-002)
- [X] T012 [US3] Implement `src/building-blocks/BuildingBlocks.AspNetCore/Permissions/PermissionCache.cs` per research.md R2 (data-model.md Cache entry `Key`, `Granted`, `ExpiresAt`) and wire it into `PermissionManager`. Then make T011 pass

**Checkpoint**: Lookups are cheap and permission changes apply on the next request

---

## Phase 6: User Story 4 - Command handler base (Priority: P2)

**Goal**: Handlers get the user, tenant, permissions, clock and database context from one base

**Independent Test**: `CommandHandlerTests`

- [X] T013 [P] [US4] Write `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Handlers/CommandHandlerTests.cs`. A `SampleHandler : CommandHandler<SampleDb>` is resolved from DI in a test endpoint for staff `u1` in tenant `acme`. It reports:
  - the user id and tenant
  - `Clock.GetUtcNow()`, which equals the fake time
  - a permission check result
  - the identity of its `Db` instance
- [X] T014 [P] [US4] Create `src/building-blocks/BuildingBlocks.Core/Handlers/IRequestContext.cs` and `CommandHandler.cs` (research.md R6, contracts/permissions-api.md)
- [X] T015 [US4] Implement `src/building-blocks/BuildingBlocks.AspNetCore/Handlers/RequestContext.cs`, and register it plus `TimeProvider.System` (`TryAdd`) in `PermissionsExtensions.cs`. Then make T013 pass

---

## Phase 7: Polish

- [X] T016 Mutation checks:
  - make `PermissionCache` always miss → the US3 tests fail
  - remove the 403 case from the exception handler → the US2 tests fail

  Then restore both
- [X] T017 Run quickstart.md steps 1–4 on a clean clone in a temporary folder and delete it. Confirm Core's nuspec still has no dependencies (SC-005 of feature 002) and that no temporary folders remain
- [X] T018 Review for comments. Commit with no attribution, push, open the PR, and confirm CI passes

---

## Dependencies & Execution Order

- Setup → Foundational → US1 → US2 → US3, which share `PermissionManager`, `PermissionsExtensions` and the exception handler → US4 → Polish
- [P]: T002, T003 and T004 together. The test-writing tasks in each story before their implementation

## Implementation Strategy

MVP is US1 plus US2: checks and the 403. US3 makes it production-ready. US4 makes it uniform for handlers.
