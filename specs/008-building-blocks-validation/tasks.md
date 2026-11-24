---

description: "Task list for feature 008 BuildingBlocks Validation"
---

# Tasks: BuildingBlocks Validation

**Input**: Design documents from `specs/008-building-blocks-validation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included (SC-004). No Docker.

**Global rules**: No comments. Pinned versions. Validators public, with no IoC dependencies.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Add FluentValidation 12.2.0 and WolverineFx.FluentValidation 6.65.1 to `src/building-blocks/Directory.Packages.props`. Create:
  - `BuildingBlocks.Validation`, referencing them and the AspNetCore project
  - `BuildingBlocks.Validation.Tests` (Exe, xunit.v3, WolverineFx.Http, WolverineFx.RuntimeCompilation, TestHost, WolverineFx.Postgresql, Npgsql and a reference to BuildingBlocks.Testing for the dead-letter test)

  Add both to `BuildingBlocks.slnx` and `InventoryAndOrder.slnx`

---

## Phase 2: Foundational (extension point, FR-006)

- [X] T002 [P] Write `src/building-blocks/BuildingBlocks.AspNetCore.Tests/Errors/ProblemMapperTests.cs`:
  - a test mapper for a custom exception yields its status, code, extensions and log level through the standard handler, with a `traceId`
  - exceptions it doesn't claim still follow the built-in table
  - with two mappers, the first one that claims the exception wins
- [X] T003 Implement `IExceptionProblemMapper.cs` and `MappedProblem.cs` (data-model.md MappedProblem `Status`, `Code`, `Detail`, `Extensions`, `LogLevel`), and have `BuildingBlocksExceptionHandler.cs` consult the registered mappers before its built-in table (research.md R2). Then make T002 and every existing 002/004 test pass

---

## Phase 3: User Story 1 - All field errors at once (Priority: P1) 🎯 MVP

**Goal**: One 400 with every failure, grouped by camel-cased field

**Independent Test**: `HttpValidationTests` and `FieldNamesTests`

- [X] T004 [P] [US1] Write `src/building-blocks/BuildingBlocks.Validation.Tests/FieldNamesTests.cs`:
  - `Name` → `name`
  - `WarehouseId` → `warehouseId`
  - `Lines[1].Sku` → `lines[1].sku`
  - `Address.PostalCode` → `address.postalCode`
  - an empty name maps to an empty key

  (research.md R3)
- [X] T005 [P] [US1] Write `src/building-blocks/BuildingBlocks.Validation.Tests/TestApp.cs`:
  - an in-memory Wolverine and TestServer app with `UseBuildingBlocksValidation`
  - a `CreateThing` command with `name`, `quantity`, `warehouseId` and `lines[]`
  - a public validator producing one error for `name`, one for `quantity` and two for `warehouseId`, plus an error on `lines[1].sku`
  - a second validator on the same command adding one more rule
  - a handler that counts invocations
  - `MapPostToWolverine<CreateThing, ThingCreated>("/things")`
- [X] T006 [US1] Write `src/building-blocks/BuildingBlocks.Validation.Tests/HttpValidationTests.cs` for spec US1 scenarios 1–4 (data-model.md "Validation problem" `type`, `title`, `status`, `code`, `errors`, `traceId`):
  - exact field keys and message counts
  - errors from both validators are combined
  - when both validators produce the same message for the same field, it appears once (E1)
  - `lines[1].sku` is present
  - a valid command returns 200
- [X] T007 [P] [US1] Implement `src/building-blocks/BuildingBlocks.Validation/FieldNames.cs` (research.md R3)
- [X] T008 [US1] Implement `ValidationProblemMapper.cs` and `ValidationExtensions.cs` (research.md R2, R4). Then make T004 and T006 pass

**Checkpoint**: The UI gets every invalid field in one response

---

## Phase 4: User Story 2 - Invalid input never reaches the handler (Priority: P1)

**Goal**: 0 handler invocations for invalid input, on HTTP and message paths

**Independent Test**: `HttpValidationTests` (invocation count) and `MessageValidationTests`

- [X] T009 [US2] Add to `HttpValidationTests.cs`: after an invalid POST, the handler invocation count is 0 (SC-002)
- [X] T010 [US2] Write `src/building-blocks/BuildingBlocks.Validation.Tests/MessageValidationTests.cs`:
  - `InvokeAsync` of an invalid `CreateThing` throws `ValidationException`, and the handler count is 0
  - an invalid message sent to a **durable** local queue (a host with PostgreSQL message storage from the feature 005 `PostgresFixture`) ends with the handler count 0, exactly one execution attempt (no retries), and exactly one row in `wolverine.wolverine_dead_letters` for that message id (D1)
  - a message type without a validator runs its handler

---

## Phase 5: User Story 3 - Same 400 for manual validation (Priority: P2)

**Goal**: A manually thrown `ValidationException` gives the identical shape, logged at information level without values

**Independent Test**: `HttpValidationTests` (manual cases)

- [X] T011 [US3] Add a `/manual` route whose handler throws `new ValidationException([...two failures...])`. Assert:
  - the same 400 shape with both fields
  - an Information log entry with the trace id and the code
  - no Error entry
  - no log entry containing the distinctive submitted value `VALUE-SECRET-42`

  (FR-005, FR-007)

---

## Phase 6: User Story 4 - One-line adoption and the boundary (Priority: P2)

- [X] T012 [US4] Confirm that `TestApp` registers no validator manually, so discovery is proven by T006. Confirm that quickstart.md states the 400-versus-422 boundary and the validator rules (spec US4)

---

## Phase 7: Polish

- [X] T013 Mutation checks:
  - remove the mapper registration → T006 fails (500 instead of 400)
  - remove the camel-case conversion → T004 and T006 fail
  - remove `UseFluentValidation()` → T009 fails (the handler runs)
  - remove the `MoveToErrorQueue` policy → T010's dead-letter case fails
  - remove `MessageFailureLogLevel(LogLevel.None)` from the validation and messaging setups → the no-values log assertions fail (research.md R6)

  Restore all three
- [X] T014 Add a `Consumer check Validation` step to `.github/workflows/building-blocks.yml` (FR-008)
- [X] T015 Run quickstart.md steps 1–3 on a clean clone in a temporary folder and delete it
- [X] T016 Review for comments. Commit with no attribution, push, open the PR, and confirm CI passes

---

## Dependencies & Execution Order

- Setup → Foundational (the extension point) → US1 → US2 → US3 → US4 → Polish
- [P]: T002 alongside T001's restore. T004, T005 and T007 in parallel

## Implementation Strategy

MVP is US1 plus the extension point. US2 proves the handler protection. US3 and US4 close the safety net and the docs.
