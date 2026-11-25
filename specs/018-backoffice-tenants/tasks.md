---

description: "Task list for feature 018 Backoffice Tenants"
---

# Tasks: Backoffice Tenants

**Input**: Design documents from `specs/018-backoffice-tenants/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui.md, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**:
- No comments.
- Every Node, npm and ng command runs in `NODE_IMAGE`.
- No icon font.
- Controls appear only from API flags.
- Paths are relative to `src/web/backoffice/src/app/` unless they start with `src/`, `tests/` or `specs/`.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Foundational

- [X] T001 [P] `problems.ts`: `problemOf(error: unknown)` returns `{ status, type, errors }` from an `HttpErrorResponse` with a problem body, and nothing otherwise. Write the tests first in `problems.spec.ts`: a problem body, a body without `errors`, a non-object body, and a non-HTTP error
- [X] T002 [P] `tenants/tenants-api.ts`: `list()` (`GET /api/identity/tenants`) and `create(body)` (`POST /api/identity/tenants`) with the types in data-model.md (TenantItem, TenantList, NewTenant). Write the tests first in `tenants/tenants-api.spec.ts` with `HttpTestingController`

---

## Phase 2: User Story 3 - Navigation fits the person signed in (P2, needed by US1)

**Goal**: a side navigation whose entries depend on the user type.

**Independent Test**: spec.md US3 scenario 1.

- [X] T003 [US3] Write the tests first in `shell/shell.spec.ts`:
  - a platform user sees "Home" and "Tenants"
  - a staff user sees only "Home"
  - the active entry is marked (`routerLinkActive`)
  - on the handset breakpoint, with a fake `BreakpointObserver`, the panel is in `over` mode and closed, and "Menu" opens it

  See them fail
- [X] T004 [US3] `shell/shell.ts`: `mat-sidenav-container` with a `mat-nav-list` from a computed entry list; responsive mode from `BreakpointObserver` with `toSignal` (research.md R2); a "Menu" button (`aria-label="Menu"`) shown on handset. `home/home.ts`: the "Manage tenants" link for the platform admin (contracts/ui.md Home). T003 green

---

## Phase 3: User Story 1 - The platform admin sees all tenants (P1) 🎯 MVP

**Goal**: the Tenants page with its states.

**Independent Test**: spec.md US1.

- [X] T005 [US1] Write the tests first in `tenants/tenants-page.spec.ts`:
  - not platform: the not-available text, and `expectNone` on the tenants URL
  - loading: the progress bar
  - loaded: a row per tenant, newest first, with the identifier, name and a formatted date
  - empty: "No tenants yet."
  - error: "Tenants could not be loaded.", and "Try again" requests again
  - "Create tenant" is shown when `canCreate` is true and hidden when false

  See them fail
- [X] T006 [US1] `tenants/tenants-page.ts` per contracts/ui.md (Tenants page) and data-model.md (TenantItem `createdAt` sorting, TenantList `actions.canCreate`); route `tenants` in `app.routes.ts`. T005 green

---

## Phase 4: User Story 2 - The platform admin creates a tenant (P1)

**Goal**: the create dialog with checks, service errors and conflicts.

**Independent Test**: spec.md US2.

- [X] T007 [US2] Write the tests first in `tenants/create-tenant-dialog.spec.ts`:
  - required fields, the identifier rule, email format and password length show the contracts/ui.md messages, and "Create" stays disabled
  - a valid submit sends the trimmed name and email and the lower-cased identifier
  - on 201 the dialog closes with the identifier
  - a 400 `validation-failed` puts each message under its field, and an unknown key goes to the top error
  - 409 `tenant-slug-taken` and `email-taken` land on their fields
  - any other failure shows the top error and the dialog stays open with the values
  - a second click while saving sends nothing
  - editing a field clears its service error
  - name over 200 and email over 256 characters show "At most 200 characters." and "At most 256 characters."
  - while a retry is needed (503 `identity-unavailable`), the dialog shows "Sign-in service is unavailable." with "Try again", which re-sends the save

  See them fail
- [X] T008 [US2] `tenants/create-tenant-dialog.ts` with Signal Forms per research.md R1 and R4 and data-model.md (NewTenant fields and checks). The page opens it, reloads on success and shows the snack bar "Tenant <identifier> created." All open dialogs close when the session leaves `signed-in`, for example after a 401 (spec.md Edge Cases). Add page tests for the reload, the snack bar and the close on sign-out. T007 green
- [X] T009 [US2] Check in a browser on the dev stack, per quickstart.md section 2:
  - the platform admin lists tenants
  - creating `demo` shows the identifier error
  - creating a new tenant works, and its admin signs in as "Staff" with only "Home"
  - staff opening `/backoffice/tenants` see the not-available text
  - the narrow-window menu works
  - no CSP errors

---

## Phase 5: End-to-end and polish

- [X] T010 `tests/e2e/specs/backoffice-tenants.spec.ts` per research.md R6. `scripts/e2e.ps1` passes all three specs, and the cleanup check is clean
- [X] T011 Lint, unit tests and build in `NODE_IMAGE` with `CI=true`. Review the new files for comments. Run the image and package pin checks
- [X] T012 [P] Record findings in `specs/018-backoffice-tenants/research.md` (R7). Update `README.md` with the Tenants page

---

## Dependencies

- T001 and T002 come first.
- US3 (T003–T004) comes before US1, because the page lives in the navigation.
- US1 (T005–T006) comes before US2 (T007–T009).
- T010 needs US2.
- Polish comes last.

## Parallel Examples

T001 with T002; T011 with T012.

## Implementation Strategy

1. Foundational, then the navigation.
2. The tenants list (MVP).
3. The create dialog.
4. The end-to-end test.
