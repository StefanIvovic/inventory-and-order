# Tasks: Tenants Paging

**Input**: Design documents from `specs/023-tenants-paging/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md, contracts/ui.md, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**:
- No comments.
- Every Node, npm and ng command runs in `NODE_IMAGE`.
- Search runs only on Enter or a button.
- Never reset the dev stack.
- Identity paths are relative to `src/services/identity/`.
- Backoffice paths are relative to `src/web/backoffice/src/app/`.

---

## Phase 1: Foundational (Identity data)

- [X] T001 `NormalizedName` on the tenant, implementing data-model.md `Tenant.NormalizedName`, per research.md R2.

  Tests first in `Identity.Tests/Tenants/TenantTests.cs` (plain unit tests): `Tenant.Create` sets `NormalizedName` to the trimmed name upper-cased invariantly, for example "Acme ltd" → "ACME LTD".

  See them fail, then add the property in `Identity.Api/Tenants/Tenant.cs`
- [X] T002 Map and migrate, per research.md R1 and R2, implementing data-model.md `Tenant.Slug` (collation `C`) and `Tenant.NormalizedName` (column, collation `C`, index `ix_tenants_normalized_name`).

  In `Identity.Api/Persistence/IdentityDbContext.cs`, then add the migration `TenantListIndexes` in `Identity.Api/Persistence/Migrations/` with `dotnet ef migrations add`. Its `Up`:
  - alters `Slug` to collation `C`
  - adds `NormalizedName` as nullable
  - backfills it with `UPDATE tenants SET "NormalizedName" = upper("Name")`
  - makes it required and creates the index

  `Down` reverses each step.

  Tests in `Identity.Tests/Tenants/TenantListMigrationTests.cs`, following `Lookups/UserTypeMigrationTests.cs` (migrate to the previous migration, insert, then migrate up):
  - a tenant inserted before the migration gets its upper-cased name
  - rolling back drops the column and restores the old `Slug` collation

---

## Phase 2: User Story 1 - Browse tenants page by page (P1) 🎯 MVP

**Independent Test**: spec.md US1.

- [X] T003 [US1] Write the service tests first in `Identity.Tests/Tenants/ListTenantsTests.cs`. Replace `PlatformAdminSeesEveryTenantNewestFirst` with:
  - tenants come in identifier byte order (`a-b` before `ab` before `b`), with `id`, `slug`, `name`, `createdAt` and `actions.canCreate`
  - `size=2` over 5 tenants gives 2, 2, 1 with `nextCursor` until the last page, which has `null`; walking every page shows each tenant exactly once (SC-003)
  - `total` is present only with `includeTotal=true`, and counts all matches
  - default size 25
  - `size=0`, `size=101` and a 201-character `q` → 400 `validation-failed`
  - a malformed `after` → 422 `invalid-cursor`
  - the 403 test for a platform user without the role stays

  See them fail
- [X] T004 [US1] Implement data-model.md `ListTenants.Q`, `.Size`, `.After`, `.IncludeTotal`, `TenantCursor.Slug`, and `TenantList.Items`, `.NextCursor`, `.Total`, `.Actions.CanCreate`, per research.md R1 and R3 and contracts/api.md. Files:
  - `Identity.Api/Features/ListTenants/ListTenants.cs`: query, `ListTenantsValidator`, `TenantCursor`, answer
  - `ListTenantsEndpoint.cs`: query parameters
  - `ListTenantsHandler.cs`: permission check, then validate, then keyset with `Take(size + 1)`, then the optional count

  T003 green (the search tests come in T007)
- [X] T005 [US1] Plan cost test `Identity.Tests/Tenants/TenantListPlanTests.cs`, per research.md R4, on 20,000 tenants:
  - with no filter, a page is one command with no sequential scan
  - a deep page costs at most twice the first

  Follow `Users/StaffListPlanTests.cs` (recorder, `EXPLAIN (ANALYZE, BUFFERS)`). If the plan is bad, fix the query or the index and record why in research.md R9
- [X] T006 [US1] Backoffice paging, per research.md R5 and contracts/ui.md, implementing data-model.md `TenantListRequest`, `TenantList`, `pageSizes` and the page state (backoffice).

  Tests first:
  - `tenants/tenants-api.spec.ts`:
    - the parameters sent: `size`, and `q`, `after` and `includeTotal` only when set
    - `{ background: true }` sets `BACKGROUND`
  - `tenants/tenants-page.spec.ts`:
    - the first page with the total, in the service's order, with no client-side sort
    - the range "1–25 of N tenants"
    - Next uses the prefetched page; the prefetch is background
    - Next asks for a page that is not cached
    - Previous shows a cached page with no request
    - Previous and Next are disabled at the ends
    - a size change restarts with the total
    - `invalid-cursor` restarts
    - a page-change failure shows the error state
    - "No tenants yet."
    - the not-available state asks for nothing
    - the existing tests for the first-load bar, the first-load error with "Try again", and Create tenant shown only with `canCreate` stay and still pass

  See them fail, then change `tenants/tenants-api.ts` and `tenants/tenants-page.ts` (copy the Users page's paging; research.md R6)

---

## Phase 3: User Story 2 - Find a tenant (P1)

**Independent Test**: spec.md US2.

- [X] T007 [US2] Service search, per research.md R2. Tests first in `ListTenantsTests.cs`:
  - `q=acme` matches the identifier `acme` and the name "Acme Holdings" of `globex`
  - `q=GLO` matches the identifier `globex` only
  - a name in other letter case matches
  - surrounding spaces are trimmed; whitespace only means no filter
  - `total` counts matches only
  - `%` and `_` in `q` are literal, not wildcards

  See them fail, then add the filter in `ListTenantsHandler.cs`. Add `q=t-1` and a name prefix to `TenantListPlanTests` (T005)
- [X] T008 [US2] Backoffice search. Tests first in `tenants-page.spec.ts`:
  - typing sends nothing
  - Enter and the search button (`aria-label="Search tenants"`) search with the trimmed text and the total
  - empty text searches without `q`
  - no match shows "No tenants match this search." and "Clear search", which restarts without `q`
  - the search box accepts at most 200 characters (`maxlength`), so the service's 400 for longer text cannot be reached from the page

  See them fail, then implement in `tenants-page.ts`

---

## Phase 4: User Story 3 - Create a tenant and see it (P2)

**Independent Test**: spec.md US3.

- [X] T009 [US3] Tests first in `tenants-page.spec.ts`: after the create dialog returns a slug, the confirmation shows and the list restarts at the first page with the total, keeping the search and size; cancelling sends nothing. See them fail, then implement in `tenants-page.ts`
- [X] T010 [US3] Update `tests/e2e/specs/backoffice-tenants.spec.ts` per research.md R7: after creating, search for the slug and expect the first row to contain it

---

## Phase 5: Polish

- [X] T011 Regenerate the static Wolverine code with `./scripts/codegen.ps1 identity` and commit `Identity.Api/Internal/Generated/`. Run `dotnet test src/services/identity/Identity.slnx` (including the no-roles route test). Check new and changed files for comments
- [X] T012 In `NODE_IMAGE` with `CI=true`: backoffice lint, unit tests and build
- [X] T013 Rebuild with `dev.ps1 up -Profile infra,apps` (never reset). Check quickstart.md section 3 in Playwright. Run `scripts/e2e.ps1`: all specs pass, and the cleanup check is clean
- [X] T014 Record findings in research.md (R9). Commit in chunks: docs; Identity (domain, migration, slice, generated code); backoffice and e2e. After the owner confirms, push, open the PR and watch CI

---

## Dependencies

- T001 → T002 → T003/T004 → T005.
- T006 depends on no earlier task: its unit tests use `HttpTestingController` and contracts/api.md. Checking it by hand needs T004.
- US2 (T007–T008) needs US1 (T004, T006): it extends the same handler and page.
- US3 (T009) needs T006. T010 needs T008.
- Polish comes last.

## Parallel Examples

- T005 (plan test) alongside T006 (backoffice paging).
- T007 (service search) alongside T008 (backoffice search) once US1 is done.

## Implementation Strategy

MVP is Phase 1 plus US1: the list pages in identifier order with a total. US2 adds search, and US3 keeps the create flow right. Ship all three in one PR.
