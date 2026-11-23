---

description: "Task list for feature 007 Permission Invalidation"
---

# Tasks: Permission Invalidation

**Input**: Design documents from `specs/007-permission-invalidation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included, against real containers (FR-007).

**Global rules**: No comments. No new packages. Nothing left behind.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Foundational

- [X] T001 [P] Create `src/building-blocks/BuildingBlocks.Messaging/PermissionAssignmentsChanged.cs` (data-model.md PermissionAssignmentsChanged `TenantId`, `UserId`, `Identity`). The constructor rejects `Guid.Empty`
- [X] T002 [P] Create `src/building-blocks/BuildingBlocks.Messaging/PermissionAssignmentsChangedHandler.cs`, which calls `IPermissionCache.Invalidate(TenantId, UserId)` (research.md R2)
- [X] T003 Create `src/building-blocks/BuildingBlocks.Messaging/PermissionInvalidationExtensions.cs` per research.md R2 and data-model.md "Broker topology per service" (exchange `Name`, instance queue `Name` and `Properties`, `Binding`)
- [X] T004 Extend the test infrastructure:
  - `TestModel.cs`: the `PermissionGrant` entity (data-model.md PermissionGrant), `TestDbContext.PermissionGrants`, a `GrantAccess(UserId, Permission, FailAfterAdd)` command and its handler, which returns `PermissionAssignmentsChanged`, and `DbPermissionProvider`, which reads grants and counts calls in a singleton `ProviderCalls`
  - `TestServiceHost.cs`:
    - options for `DurabilityMode`, the provider (static or DB-backed) and `UseBuildingBlocksPermissionInvalidation`
    - a `GET /permissions` endpoint returning the granted set
    - an overload that starts a second instance on an existing database and service name

---

## Phase 2: User Story 1 - Every instance sees the change (Priority: P1) 🎯 MVP

**Goal**: A committed role change invalidates the user's cache on all instances of the service, and only that user's entry

**Independent Test**: `PermissionInvalidationTests`, US1 cases

- [X] T005 [US1] Write `src/building-blocks/BuildingBlocks.Messaging.Tests/PermissionInvalidationTests.cs`, US1 cases. Instances A and B of service `inv` share one database, in Balanced mode, plus service `other`:
  - warm `u1` and `u2` on A and B through `/permissions`
  - grant `u1` a permission on A for `acme`
  - wait until both A and B serve `u1` the new set: each instance makes exactly one more provider call, and `/permissions` shows the new permission
  - `u2` stays cached, with no extra calls
  - `other`, warmed for `u1`, makes no extra call
  - a new instance C started afterwards returns the new set
  - a platform user without a tenant (`TenantId = null`, cache key `perm::{user}`) is invalidated after a grant (spec edge case, data-model.md PermissionAssignmentsChanged `TenantId`)

  (SC-001, SC-003)
- [X] T006 [US1] Make T005 pass. Wolverine findings (inbox on exclusive queues, binding behaviour) go into research.md R4

**Checkpoint**: Role changes apply on every instance

---

## Phase 3: User Story 2 - Only after commit (Priority: P1)

**Goal**: A failed change invalidates nothing. A successful one is visible on reload

**Independent Test**: `PermissionInvalidationTests`, US2 cases

- [X] T007 [US2] Add the US2 cases to `PermissionInvalidationTests.cs`:
  - `GrantAccess` with `FailAfterAdd = true` → after a settle period, A and B make 0 extra provider calls, the grant table is unchanged, and the tracked session shows no `PermissionAssignmentsChanged` sent
  - after a successful grant, the reload on B contains the committed permission. This covers US2 scenario 2, and scenario 3 is covered by feature 006's outage test of the same outbox path

  (SC-002)

---

## Phase 4: User Story 3 - One rule, enforced by design (Priority: P2)

**Goal**: The constitution and docs state the only supported pattern

**Independent Test**: quickstart.md step 4, plus the queue-cleanup assertion

- [X] T008 [US3] Amend `.specify/memory/constitution.md` to v1.2.0 per research.md R6:
  - the invalidation rule under VI
  - `MessageHandler<TDbContext>` / `IEventContext` in III
  - the code-generation constraint in Architecture Constraints
  - the version and last-amended date

  Remove the Sync Impact Report before committing
- [X] T009 [US3] Add to `PermissionInvalidationTests.cs`: after instance B is stopped, `QueueDeclarePassiveAsync` on its instance queue fails with NOT_FOUND (SC-004, FR-004)

---

## Phase 5: Polish

- [X] T010 Mutation checks:
  - remove the binding of the instance queue → T005 fails
  - the grant handler calls `IPermissionCache.Invalidate` directly before returning → T007's failure case fails
  - make the queue non-exclusive and non-auto-delete → T009 fails, or the broker rejects the queue
  - remove `BufferedInMemory()` from the instance queue → T005 fails, which confirms the root cause in research.md R4a

  Restore all three
- [X] T011 Run quickstart.md steps 1–4 on a clean clone in a temporary folder and delete it. The cleanup check must be green
- [X] T012 Review for comments. Commit with no attribution, push, open the PR, and confirm CI passes

---

## Dependencies & Execution Order

- Phase 1 → US1 → US2 (same test file) → US3 → Polish
- [P]: T001 with T002

## Implementation Strategy

MVP is US1. US2 proves the ordering guarantee. US3 makes the rule binding.
