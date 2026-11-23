---

description: "Task list for feature 003 BuildingBlocks EF Tenancy"
---

# Tasks: BuildingBlocks EF Tenancy

**Input**: Design documents from `specs/003-building-blocks-ef-tenancy/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. SC-004 requires every acceptance scenario to be automated.

**Global rules**: No comments. Versions are pinned (research.md). No Docker.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Add `Microsoft.EntityFrameworkCore` 10.0.15 and `Microsoft.EntityFrameworkCore.Sqlite` 10.0.15 as `PackageVersion` entries in `src/building-blocks/Directory.Packages.props`
- [X] T002 Create the projects:
  - `src/building-blocks/BuildingBlocks.EntityFrameworkCore/BuildingBlocks.EntityFrameworkCore.csproj`, referencing `Microsoft.EntityFrameworkCore` and the Core project
  - `src/building-blocks/BuildingBlocks.EntityFrameworkCore.Tests/BuildingBlocks.EntityFrameworkCore.Tests.csproj`: `OutputType` Exe, xunit.v3, `Using Xunit`, Sqlite, and a reference to the EF project

  Add both to `src/building-blocks/BuildingBlocks.slnx` and `InventoryAndOrder.slnx`

---

## Phase 2: Foundational

- [X] T003 Create `src/building-blocks/BuildingBlocks.Core/Tenancy/ITenantOwned.cs` (data-model.md ITenantOwned `TenantId`, research.md R1)
- [X] T004 [P] Create `src/building-blocks/BuildingBlocks.EntityFrameworkCore/TenantFilter.cs` with `Name` and `TenantIdMaxLength` (data-model.md Tenant filter `Name`, ITenantOwned `TenantId` max length) and `TenantViolationException.cs` (contracts/ef-api.md)
- [X] T005 Create the test model `src/building-blocks/BuildingBlocks.EntityFrameworkCore.Tests/TestModel.cs`:
  - `Order` and `OrderLine`, tenant-owned, with a navigation between them
  - `Country`, not tenant-owned
  - `Note`, tenant-owned, with an extra `"SoftDelete"` named filter
  - `TestDbContext : TenantDbContext`

  Also create `SqliteFixture.cs`, which opens one in-memory connection per test, creates the schema, and exposes `NewContext(string? tenant)` and `Seed(...)` (research.md R7)

---

## Phase 3: User Story 1 - Reads only ever see the current tenant (Priority: P1) 🎯 MVP

**Goal**: Every query on tenant-owned entities is restricted to the current tenant, and fails closed with no tenant

**Independent Test**: `QueryFilterTests`

- [X] T006 [P] [US1] Write `src/building-blocks/BuildingBlocks.EntityFrameworkCore.Tests/QueryFilterTests.cs`. Seed `acme` and `globex` rows, then assert for `acme`:
  - list, `FindAsync` by a `globex` id (null), count, `Include` of lines, a join to lines, and a projection all return only `acme` data
  - no tenant returns zero rows
  - `Country` is unfiltered
  - the generated SQL contains a tenant parameter (SC-001)
- [X] T007 [US1] Implement `TenantDbContext.OnModelCreating` in `src/building-blocks/BuildingBlocks.EntityFrameworkCore/TenantDbContext.cs`:
  - for every entity type implementing `ITenantOwned`, apply the named filter `e.TenantId == CurrentTenantId` through a generic helper (research.md R3, data-model.md Tenant filter `Predicate`)
  - configure the column as required, `HasMaxLength(TenantFilter.TenantIdMaxLength)` and indexed (research.md R6, data-model.md ITenantOwned `TenantId`)

  Then make T006 pass

**Checkpoint**: Reads are isolated per tenant

---

## Phase 4: User Story 2 - Writes are stamped and guarded (Priority: P1)

**Goal**: New rows get the context tenant, and every invalid tenant write fails the whole save

**Independent Test**: `WriteGuardTests`

- [X] T008 [P] [US2] Write `src/building-blocks/BuildingBlocks.EntityFrameworkCore.Tests/WriteGuardTests.cs`, one test per row of the data-model.md write-guard table:
  - stamp on insert, accept a matching tenant, reject a different tenant, reject with no tenant, reject a changed tenant, accept a delete
  - a mixed batch with one invalid entity persists nothing, checked with a fresh context
  - both `SaveChanges` and `SaveChangesAsync` are covered (SC-002)
- [X] T009 [US2] Implement `src/building-blocks/BuildingBlocks.EntityFrameworkCore/TenantSaveChangesInterceptor.cs` per research.md R5 and the data-model.md write-guard decisions (ITenantOwned `TenantId` set-once rule). Add it in `TenantDbContext.OnConfiguring`. Then make T008 pass

**Checkpoint**: No write can put data under the wrong tenant

---

## Phase 5: User Story 3 - Deliberate cross-tenant access (Priority: P2)

**Goal**: An explicit per-query bypass of the tenant filter only

**Independent Test**: `BypassTests`

- [X] T010 [P] [US3] Write `src/building-blocks/BuildingBlocks.EntityFrameworkCore.Tests/BypassTests.cs`:
  - `AcrossAllTenants()` returns both tenants
  - the next normal query is filtered again
  - on `Note`, `AcrossAllTenants()` still excludes soft-deleted rows
- [X] T011 [US3] Implement `src/building-blocks/BuildingBlocks.EntityFrameworkCore/TenantQueryableExtensions.cs` with `IgnoreQueryFilters([TenantFilter.Name])` (research.md R4). Then make T010 pass

---

## Phase 6: User Story 4 - One-line adoption (Priority: P2)

**Goal**: Deriving from the base context and marking entities is all it takes

**Independent Test**: `AdoptionTests`, plus quickstart steps 2–4

- [X] T012 [US4] Write `src/building-blocks/BuildingBlocks.EntityFrameworkCore.Tests/AdoptionTests.cs`. It asserts from the built model that:
  - every `ITenantOwned` entity has the `"Tenant"` filter, a required `TenantId` with max length 64, and an index
  - `Country` has none of these
- [X] T013 [US4] Run `pwsh scripts/pack.ps1` and quickstart.md steps 2–4:
  - all three packages share one version, with no Tests packages
  - the EF nuspec depends on Core and Microsoft.EntityFrameworkCore 10.0.15 only
  - the consumer check passes

---

## Phase 7: Polish

- [X] T014 Change the consumer check step in `.github/workflows/building-blocks.yml` to `-PackageId BuildingBlocks.EntityFrameworkCore`, which restores Core transitively. Keep the AspNetCore check as a second step
- [X] T015 Mutation-check that the tests have teeth:
  - remove the filter application → US1 tests fail
  - remove the interceptor registration → US2 tests fail

  Then restore both
- [X] T016 Run the full quickstart on a clean clone in a temporary folder and delete it, with no temporary folders left. Review for comments. Commit with no attribution, push, open the PR, and confirm CI passes

---

## Dependencies & Execution Order

- Setup → Foundational → US1 → US2 (both touch `TenantDbContext.cs`) → US3 → US4 → Polish
- [P]: T004 with T003. The test-writing tasks T006, T008 and T010 can be drafted in parallel

## Implementation Strategy

MVP is US1, read isolation. US2 completes the safety guarantee. US3 and US4 make it usable and verifiable.
