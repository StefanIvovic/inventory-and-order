---

description: "Task list for feature 002 BuildingBlocks Core"
---

# Tasks: BuildingBlocks Core

**Input**: Design documents from `specs/002-building-blocks-core/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. SC-001 requires every acceptance scenario to be covered by an automated test.

**Global rules**: No comments in any file. Every version is pinned (research.md). No Docker.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Change `src/building-blocks/Directory.Build.props` so `IsPackable` is `true` only when `$(MSBuildProjectName)` does not end in `.Tests` (research.md R8)
- [X] T002 [P] Add `Microsoft.AspNetCore.TestHost` 10.0.15 and `xunit.v3` 4.0.3 as `PackageVersion` entries in `src/building-blocks/Directory.Packages.props` (research.md R8)
- [X] T003 Create the projects, each with no version properties:
  - `src/building-blocks/BuildingBlocks.AspNetCore/BuildingBlocks.AspNetCore.csproj`: `FrameworkReference Microsoft.AspNetCore.App`, plus a `ProjectReference` to Core (research.md R1)
  - `src/building-blocks/BuildingBlocks.Core.Tests/BuildingBlocks.Core.Tests.csproj`: `OutputType` Exe, xunit.v3, `Using Xunit`, reference to Core
  - `src/building-blocks/BuildingBlocks.AspNetCore.Tests/BuildingBlocks.AspNetCore.Tests.csproj`: the same, plus TestHost and a reference to AspNetCore
- [X] T004 Add the three new projects to `src/building-blocks/BuildingBlocks.slnx` and to `InventoryAndOrder.slnx`

---

## Phase 2: Foundational

- [X] T005 [P] Create the claim constants `src/building-blocks/BuildingBlocks.Core/Identity/AppClaimTypes.cs` (data-model.md AppClaimTypes `Subject`, `TenantId`, `UserType`) and the enum `src/building-blocks/BuildingBlocks.Core/Identity/UserType.cs` (data-model.md UserType)
- [X] T006 Create the `src/building-blocks/BuildingBlocks.AspNetCore.Tests/TestAuthHandler.cs` authentication scheme. It authenticates only when an `X-Test-Claims` header is present, and builds the principal from `name=value;...` pairs. Also create a `TestApp` helper that builds a TestServer with `AddBuildingBlocks()`, the test scheme, `UseAuthentication()`, `UseBuildingBlocks()` and configurable endpoints

---

## Phase 3: User Story 1 - Know who is calling (Priority: P1) 🎯 MVP

**Goal**: A typed current user, with malformed identities rejected

**Independent Test**: The US1 tests in both test projects

### Tests for User Story 1

- [X] T007 [P] [US1] Write `src/building-blocks/BuildingBlocks.Core.Tests/Identity/CurrentUserTests.cs`, covering `CurrentUser.TryCreate`:
  - valid staff, customer and platform principals
  - platform without a tenant, and platform with a tenant
  - `user_type` in any letter case
  - invalid `sub`, unknown or missing `user_type`, and staff or customer without a tenant, each producing a rejection reason
  - an empty `tenant_id`, which is rejected (data-model.md CurrentUser `UserId`, `TenantId`, `UserType`)
- [X] T008 [P] [US1] Write `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Identity/IdentityMiddlewareTests.cs`:
  - an endpoint echoes `ICurrentUser`: a valid staff identity is returned
  - an unauthenticated request gives `IsAuthenticated=false`, and reading `UserId` throws
  - a malformed `sub`, an unknown `user_type`, and staff without a tenant each give 401 with problem `code` `invalid-identity` and `traceId`, and the endpoint is not invoked

### Implementation for User Story 1

- [X] T009 [P] [US1] Create the interface `src/building-blocks/BuildingBlocks.Core/Identity/ICurrentUser.cs` and the record `src/building-blocks/BuildingBlocks.Core/Identity/CurrentUser.cs` with `TryCreate`, implementing data-model.md CurrentUser `UserId`, `TenantId` and `UserType` (contracts/core-api.md)
- [X] T010 [US1] Create `src/building-blocks/BuildingBlocks.AspNetCore/Identity/HttpCurrentUser.cs`, which reads the `CurrentUser` feature. When it is absent it reports unauthenticated, and `UserId` and `UserType` throw `InvalidOperationException`
- [X] T011 [US1] Create `src/building-blocks/BuildingBlocks.AspNetCore/Identity/IdentityMiddleware.cs` (research.md R5). It logs a Warning without claim values, and returns 401 through `IProblemDetailsService` with `code` `invalid-identity` (contracts/problem-details.md)
- [X] T012 [US1] Create `src/building-blocks/BuildingBlocks.AspNetCore/BuildingBlocksExtensions.cs`, with `AddBuildingBlocks` registering the identity services and problem details, and `UseBuildingBlocks` adding `IdentityMiddleware` (contracts/aspnetcore-api.md). Then make T007 and T008 pass

**Checkpoint**: Handlers get a typed current user, and bad identities never reach them

---

## Phase 4: User Story 2 - Know which tenant a request belongs to (Priority: P1)

**Goal**: A set-once tenant context, populated per request

**Independent Test**: The US2 tests

### Tests for User Story 2

- [X] T013 [P] [US2] Write `src/building-blocks/BuildingBlocks.Core.Tests/Tenancy/TenantContextTests.cs`:
  - unset state
  - set once
  - setting the same value again is a no-op
  - setting a different value throws, and the context keeps its value
  - setting null or whitespace throws (data-model.md TenantContext `TenantId`, `HasTenant`, transitions)
- [X] T014 [P] [US2] Write `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Tenancy/TenantResolutionTests.cs`:
  - a staff `acme` request sees `acme`
  - a platform request without a tenant sees `HasTenant=false`
  - 50 concurrent requests alternating `acme` and `globex` each see their own tenant

### Implementation for User Story 2

- [X] T015 [P] [US2] Create `src/building-blocks/BuildingBlocks.Core/Tenancy/ITenantContext.cs` and `TenantContext.cs` (research.md R6, data-model.md TenantContext `TenantId`)
- [X] T016 [US2] Register `TenantContext` as scoped, both as itself and as `ITenantContext`, in `BuildingBlocksExtensions.cs`. Have `IdentityMiddleware.cs` call `Set` when a tenant is present. Then make T013 and T014 pass

**Checkpoint**: Every authenticated request runs with exactly one tenant

---

## Phase 5: User Story 3 - Consistent error responses (Priority: P1)

**Goal**: Domain, not-found and unexpected errors become standard problem responses

**Independent Test**: The US3 tests

### Tests for User Story 3

- [X] T017 [P] [US3] Write `src/building-blocks/BuildingBlocks.Core.Tests/Errors/DomainErrorTests.cs`:
  - a null or whitespace code throws `ArgumentException`
  - kind, code and message are kept
  - `NotFoundException` has code `not-found` and the generic message (data-model.md DomainException `Code`, `Kind`, `Message`; NotFoundException `Code`, `Resource`, `Message`)
- [X] T018 [P] [US3] Write `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Errors/ErrorMappingTests.cs`:
  - Conflict → 409, Rule → 422, NotFound → 404, each with `code`, `detail`, `traceId` and `application/problem+json`
  - a generic exception with the distinctive message `SECRET-123` → 500, and the body contains no `SECRET-123`, no stack trace and no `code`
  - an exception thrown by middleware before authentication is still mapped
  - log levels per contracts/problem-details.md, checked with a capturing `ILoggerProvider`, and every logged entry carries the trace id

### Implementation for User Story 3

- [X] T019 [P] [US3] Create `src/building-blocks/BuildingBlocks.Core/Errors/DomainErrorKind.cs`, `DomainException.cs` and `NotFoundException.cs` (data-model.md DomainException `Code`, `Kind`; NotFoundException `Code`, `Resource`, `Message`; research.md R7)
- [X] T020 [P] [US3] Create `src/building-blocks/BuildingBlocks.AspNetCore/Errors/ProblemDetailsSetup.cs`, a `CustomizeProblemDetails` hook that adds `traceId` (research.md R3)
- [X] T021 [US3] Create `src/building-blocks/BuildingBlocks.AspNetCore/Errors/BuildingBlocksExceptionHandler.cs`, an `IExceptionHandler` implementing the status, `code` and log-level table in contracts/problem-details.md (research.md R4)
- [X] T022 [US3] Create `src/building-blocks/BuildingBlocks.AspNetCore/ExceptionHandlerStartupFilter.cs`, which runs `UseExceptionHandler()` first. Register the handler, the startup filter and problem details in `AddBuildingBlocks` (research.md R2). Then make T017 and T018 pass

**Checkpoint**: Every service returns identical error shapes

---

## Phase 6: User Story 4 - One-line adoption (Priority: P2)

**Goal**: The packages work from the feed with 2 lines of setup

**Independent Test**: quickstart.md steps 2–5

- [X] T023 [US4] Write `src/building-blocks/BuildingBlocks.AspNetCore.Tests/AdoptionTests.cs`. It asserts that a TestServer configured with exactly `AddBuildingBlocks()`, `UseAuthentication()` and `UseBuildingBlocks()` serves US1–US3 behaviour together in one request flow (SC-002)
- [X] T024 [US4] Run `pwsh scripts/pack.ps1` and quickstart.md steps 2–5:
  - both packages share one version, and there is no Tests package
  - Core's nuspec has no dependencies (SC-005)
  - AspNetCore depends on Core at the same version
  - the consumer check for `BuildingBlocks.AspNetCore` passes

---

## Phase 7: Polish & Cross-Cutting

- [X] T025 Add the step `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` to `.github/workflows/building-blocks.yml`, after the repository tool tests
- [X] T026 Run all quickstart.md steps on a clean clone in a temporary folder, then delete it. Run the image check (exit 0). Confirm no temporary folders remain
- [X] T027 Review every added file for comments. Commit with no attribution, push, open the PR with the validation results, and confirm CI passes

---

## Dependencies & Execution Order

- Setup (T001–T004) → Foundational (T005, T006) → US1, US2 and US3
- US2 extends files from US1 (`BuildingBlocksExtensions.cs`, `IdentityMiddleware.cs`), so run US1 before US2
- US3 is independent of US1 and US2 apart from `BuildingBlocksExtensions.cs`. Run it after US1
- US4 after US1–US3. Polish last

### Parallel Opportunities

- T002 with T001
- Within each story, the tests marked [P] and the Core types marked [P]

## Implementation Strategy

MVP is US1. Then US2 and US3, which complete the core, then US4 adoption and packaging, then CI and the PR.
