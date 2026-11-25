# Tasks: Lookups

**Input**: Design documents from `specs/020-lookups/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/lookups-api.md, quickstart.md

**Tests**: required by the constitution (principle VII) and the spec (SC-001 to SC-005). Write them first and watch them fail.

## Phase 1: Setup

- [X] T001 Amend `.specify/memory/constitution.md` to v1.6.0 with `/speckit-constitution`, following research.md R8:
  - the lookup rule as an Architecture Constraints row
  - the seeding rule
  - the `SignedIn` category in principle VI, with its 401 route check

  Commit on its own.
- [X] T002 Create `src/building-blocks/BuildingBlocks.Lookups/` and `BuildingBlocks.Lookups.Tests/`:
  - references to AspNetCore, EntityFrameworkCore and Validation, plus the analyzers, like the sibling packages
  - add both to `BuildingBlocks.slnx` and `Directory.Packages.props`
  - check that `scripts/pack.ps1` and `building-blocks.yml` pick the new project up

## Phase 2: Foundational (BuildingBlocks 0.4.0; blocks all stories)

- [X] T003 [P] `SignedIn` route category in `BuildingBlocks.AspNetCore/Authentication/`, per research.md R5: `RouteCategory.SignedIn`, the `[SignedIn]` attribute, `.RequireSignedIn()` and the `signed-in` policy.

  Tests first, in `BuildingBlocks.AspNetCore.Tests/Authentication/`:
  - staff, customer and platform get 200
  - anonymous gets 401
  - a malformed identity gets 401
  - combining it with the allow-list or with platform-only fails at startup
- [X] T004 [P] `NoRolesRouteCheck` and `RouteCheckReport` in `BuildingBlocks.Testing/Routes/`: `SignedIn` routes are called without a token and must answer 401, and they are reported in a new `SignedIn` list. Tests first, in `BuildingBlocks.Testing.Tests/NoRolesRouteCheckTests.cs`: a `SignedIn` route answering 200 anonymously fails the check, and one answering 401 is listed
- [X] T005 Entities `LookupType` and `LookupValue`, and `ModelBuilder.ApplyLookups()`, following data-model.md:
  - `LookupType.Id`, `.Code` and `.Name`
  - `LookupValue.Id`, `.Code`, `.Name` and `.LookupTypeId`
  - the R2 table names, constraints and `ix_lookup_values_type_name`

  Also the definition types `LookupCatalog`, `LookupTypeDefinition` and `LookupValueDefinition`, and `LookupCatalog.Validate()`.

  Tests first:
  - duplicate ids are refused
  - a bad code format is refused (data-model.md `LookupType.Code`, `LookupValue.Code`)
  - a duplicate code within a type is refused
  - a name over 200 characters is refused (data-model.md `LookupType.Name`, `LookupValue.Name`)
  - the model creates the constraints on a test DbContext
- [X] T006 `LookupSeeder<TDbContext>` and `LookupSeedException`, per research.md R3. Tests first, on Testcontainers with a test DbContext and one database per test:
  - an empty database gets every defined row
  - a changed name is corrected and keeps its id
  - an extra row is left alone
  - a second run changes nothing
  - two concurrent runs both succeed with one row each
  - a hand-made row holding a defined code under another id makes seeding fail
  - a defined value still missing afterwards throws
- [X] T007 `LookupReader`, `LookupValueQuery`, `LookupCursor`, `LookupValuePage` and `LookupValueQueryValidator`, per research.md R4 and data-model.md:
  - `ListLookupValues.typeCode`, `.q`, `.ids`, `.codes`, `.size` and `.after`
  - `LookupValuePage.items` and `.nextCursor`

  Tests first:
  - sorted by name then id
  - `q` matches name ignoring case or code, and `%` and `_` are taken literally
  - `ids`, `codes` and their combination with `q`
  - pages walk with no gaps or repeats, and the last page has no cursor
  - the validator refuses size 0 and 201, 201 ids, 201 codes, `q` of 101 characters and a malformed `after`
  - an unknown type gives `lookup-type-not-found`
  - an empty result is an empty page
  - one database command per page
- [X] T008 [P] `UseLookupCaching()` and `.WithLookupCaching()` in `LookupCaching.cs`, per research.md R6. Tests first:
  - the ETag is stable for the same body and changes when the body changes
  - a matching `If-None-Match` gives 304 with no body, and `*` matches
  - `Cache-Control: private, max-age=300` and `Vary` are set
  - endpoints without the metadata are untouched
  - non-200 answers carry no ETag
- [ ] T009 Run the full BuildingBlocks suite, `pack.ps1`, `check-consumer.ps1`, `check-analyzers.ps1` and `check-test-cleanup.ps1`. Commit. **After the owner confirms**, tag `building-blocks/v0.4.0` locally

**Checkpoint**: BuildingBlocks 0.4.0 is packed in `artifacts/packages/`.

## Phase 3: User Story 3, Identity stores the user type as a lookup reference (P1)

**Goal**: `identity_users.UserTypeId` references a `user-type` value. Existing users keep their type, and nothing visible changes.

**Independent Test**: the migration test, plus the full existing Identity suite.

- [X] T010 [US3] Pin BuildingBlocks 0.4.0 and add `BuildingBlocks.Lookups` in `src/services/identity/Directory.Packages.props` and `Identity.Api.csproj`. Run `restore-feed.ps1 -Service identity`. The build and the existing suite stay green
- [X] T011 [US3] Add `Identity.Api/Lookups/IdentityLookups.cs`, the catalog from data-model.md (Identity catalog) with fixed GUIDs. `Users/UserTypes.cs` becomes `LookupValueDefinition`s with `Code` constants, and its callers are updated (`AppUser`, `ClientPolicies`, the handlers, the seeders, the tests). Test: `IdentityLookups.Catalog.Validate()` passes
- [X] T012 [US3] Write the migration test first, `Identity.Tests/Lookups/UserTypeMigrationTests.cs`:
  1. migrate to the migration before `UserTypeLookup`
  2. insert a platform user, staff and a customer with raw SQL
  3. migrate to the latest

  Then assert every type code and every other column unchanged. Also assert that the database refuses a `UserTypeId` holding a `staff-status` id, and a random id.

  See it fail.
- [X] T013 [US3] Change `IdentityDbContext` for data-model.md "AppUser change" (`AppUser.UserTypeId`: uuid, not null, references `LookupValue.Id`), per research.md R7:
  - `ApplyLookups()`
  - map `AppUser.UserType` to `UserTypeId` with the code-to-id value converter
  - FK `fk_identity_users_user_type`
  - `ck_identity_users_type_tenant` on the ids
  - the two filtered email indexes on `UserTypeId`

  Generate the migration `UserTypeLookup` and hand-edit it with the R7 steps (insert the user-type rows, add the column as nullable, backfill, make it NOT NULL, drop the old column, recreate the rules), with a matching `Down`. T012 green, and the full Identity suite green

## Phase 4: User Story 2, Identity starts with the lookups it needs (P1)

**Goal**: every start leaves exactly the defined lookups, with fixed ids.

**Independent Test**: start Identity on an empty, a changed and an already-seeded database.

- [X] T014 [US2] Write the tests first in `Identity.Tests/Lookups/LookupSeedingTests.cs`:
  - a fresh start has both types and their five values, with the catalog ids
  - after a name is edited by hand, the restart restores it
  - a second start changes nothing
  - a database upgraded through T012's path (which already holds the `user-type` rows) also gets the `staff-status` rows, with the user-type ids unchanged (SC-003)

  Then call `LookupSeeder` in `Persistence/StartupTasks.cs` right after `MigrateAsync`, before the other seeders.

## Phase 5: User Story 1, screens read a value list by code (P1)

**Goal**: `GET /lookups/{typeCode}/values` works for every signed-in user, with filters, paging and 304.

**Independent Test**: the endpoint tests for the three user types and an anonymous caller.

- [X] T015 [US1] Write the tests first in `Identity.Tests/Lookups/LookupValuesEndpointTests.cs`, per contracts/lookups-api.md:
  - staff, customer and platform tokens each read `user-type` (3 values, sorted by name) and `staff-status` (2 values)
  - anonymous gets 401
  - `nope` gets 404 `lookup-type-not-found`
  - `q`, `ids`, `codes` and `size` with `after` paging work
  - an invalid filter gives 400 `validation-failed` with the field
  - the second request with the ETag gets 304 and no body

  See them fail.
- [X] T016 [US1] Add `Identity.Api/Features/Lookups/`: the `ListLookupValues` query, handler (it calls `LookupReader`), validator registration and endpoint `[WolverineGet("/lookups/{typeCode}/values")]` with `[SignedIn]` and `.WithLookupCaching()`. Add `UseLookupCaching()` in `Program.cs`. T015 green
- [X] T017 [US1] The no-roles route test in Identity passes, and its report lists the lookup route under `SignedIn`. Regenerate the Wolverine code with `scripts/codegen.ps1 -Service identity`, and `-Check` passes

## Phase 6: Polish

- [X] T018 Run `dev.ps1 up -Profile infra,apps` (never reset) and check quickstart section 3: the existing dev users migrate and sign in, the values come back, and a reload gives 304
- [X] T019 Run `scripts/e2e.ps1`: all specs pass, and the cleanup check finds nothing named `e2e-test`
- [ ] T020 Commit in chunks: docs, constitution, BuildingBlocks, Identity. **After the owner confirms**, push the branch and the tag `building-blocks/v0.4.0`, open the PR, and watch `building-blocks.yml`, `identity.yml`, `e2e.yml` and `deploy.yml`

## Dependencies & Execution Order

- T001 → T002 → Phase 2. T003, T004 and T008 can run in parallel. T005 → T006 → T007. T009 closes the phase.
- Identity work needs the 0.4.0 packages: T009 → T010.
- **Story order**: US3 (T011–T013), then US2 (T014), then US1 (T015–T017).
  - US2's seeding needs the tables that the US3 migration creates.
  - US1 reads the seeded values.

  All three are P1. This order follows the data dependencies, not their importance.
- Polish comes last.

## Parallel Opportunities

- T003, T004 and T008 touch different packages and folders.
- T012's test can be written while T011 is in progress.

## Implementation Strategy

- One PR. BuildingBlocks first, so the packages exist, then Identity.
- **Owner confirmations**: the 0.4.0 tag (T009), then the push with the tag and the PR (T020).
- 019 resumes after this PR merges, and uses the `staff-status` lookup for its filter.
