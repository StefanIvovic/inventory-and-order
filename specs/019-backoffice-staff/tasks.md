---

description: "Task list for feature 019 Backoffice Staff"
---

# Tasks: Backoffice Staff

**Input**: Design documents from `specs/019-backoffice-staff/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui.md, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**:
- No comments.
- Every Node, npm and ng command runs in `NODE_IMAGE`.
- Controls appear only from API flags.
- Test fixtures copy the real problem shape (018 R7): `{ type: <RFC URL>, title, status, code, errors? }`.
- Never reset the dev stack.
- Paths are relative to `src/web/backoffice/src/app/` unless they start with `tests/` or `specs/`.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 0: Identity staff list paging (FR-011 to FR-016)

- [ ] T101 Write the tests first in `src/services/identity/Identity.Tests/Users/StaffListPagingTests.cs` (Testcontainers):
  - default size 25, sorted by email then id
  - `nextCursor` walks all pages without gaps or repeats, and the last page has none
  - each filter (`q` prefix case-insensitive, `status`, `role`) and their combination
  - `size` 0 and 101, `q` over 256 characters, an unknown `status` (for example `any`) and an unknown role give 400
  - a tampered `after` gives 422 `invalid-cursor`
  - `includeTotal` gives the matching count, and is absent otherwise
  - per-row and list flags are unchanged
  - other tenants' staff and customers never appear

  See them fail
- [ ] T102 `Features/ListUsers/`: query record with filters, cursor encoding, validator and handler per research.md R9 (data-model.md Staff list request and StaffList `nextCursor`, `total`), and the endpoint with query parameters. Roles are projected in the same query. T101 green
- [ ] T103 Write the plan and cost tests first in `StaffListPagingTests`:
  - seed 50,000 staff with a bulk insert
  - `EXPLAIN` a deep page and each filter: an index scan on `ix_identity_users_staff_list` and no `Seq Scan`
  - an EF command interceptor counts exactly 1 command per page with a warm permission set

  See them fail, then add the indexes and the migration `StaffListIndexes` per R9 (raw SQL if the builder can't set the collation)
- [ ] T104 Regenerate the Wolverine code with `scripts/codegen.ps1 -Service identity` and check it with `-Check`. The full Identity suite, including the no-roles route test, stays green

---

## Phase 1: Foundational

- [ ] T001 Identity `GET /roles` display names per research.md R2 and data-model.md (Role `displayName`, `permissions`): a display name next to each entry of `IdentityRoles` and `IdentityPermissions`, sorted by display name. Tests first in `src/services/identity/Identity.Tests/RolesTests.cs`: every tenant role and permission has a display name, the answer shape, and the sort order. Regenerate Wolverine code if the endpoint changes
- [ ] T002 [P] `users/users-api.ts` (list with filters, `size`, `after` and `includeTotal` as query parameters; create, disable, assign, remove) `roles/roles-api.ts` (list) and `lookups/lookups-api.ts` (values of one type by code), with the data-model.md types (Staff list request, StaffItem, StaffList, NewStaffUser, Role). Tests first in `users/users-api.spec.ts` and `roles/roles-api.spec.ts`: method, URL, query string (empty filters omitted), body, and role names encoded in the path
- [ ] T003 [P] `confirm-dialog.ts`: title, text and confirm label as dialog data; returns `true` on confirm. Tests first in `confirm-dialog.spec.ts`
- [ ] T004 `shell/shell.ts`: staff entries Home, Users and Roles (FR-001). Update the 018 navigation tests in `shell/shell.spec.ts` first. Lazy routes `users` and `roles` in `app.routes.ts`

---

## Phase 2: User Story 1 - Staff see who works in their tenant (P1) 🎯 MVP

**Independent Test**: spec.md US1.

- [ ] T005 [US1] Write the tests first in `users/users-page.spec.ts`:
  - the platform admin gets the not-available text and no request
  - loading
  - loaded, sorted by email, with the role display names from `/roles`, "No roles", the status lookup names and the date
  - the staff page, `/roles` and the status lookup are requested together; if either list fails the page shows the error state, and a 403 on `/roles` shows no-access
  - 403 shows "You don't have access to users."
  - error and "Try again"
  - "Add staff user" by `canCreate`; per-row "Roles" by `canAssignRoles` and "Disable" by `canDisable`
  - paging per research.md R10:
    - the first request asks for the total
    - the range text is right
    - Next shows the prefetched page without a new request when it is cached
    - Previous reuses the seen token
    - the size change and every filter restart with the total
    - the search waits 300 ms (fake timers)
    - nothing matching shows the "No users match" state and "Clear filters"
    - the status options come from the `staff-status` lookup and the role options from the roles list, with no option written in the UI
    - 422 `invalid-cursor` restarts at the first page

  See them fail
- [ ] T006 [US1] `users/users-page.ts` per contracts/ui.md (Users page) and data-model.md (StaffItem fields and flags, StaffList `actions.canCreate`). T005 green

---

## Phase 3: User Story 2 - An administrator adds a staff user (P1)

**Independent Test**: spec.md US2.

- [ ] T007 [US2] Write the tests first in `users/add-staff-dialog.spec.ts`, mirroring 018's dialog tests with the contracts/ui.md messages:
  - the checks before sending
  - the trimmed email
  - success closes with the email
  - field errors from a real-shaped 400
  - `email-taken` under the email
  - other failures at the top
  - no double submit
  - the retry line

  See them fail
- [ ] T008 [US2] `users/add-staff-dialog.ts` (Signal Forms, research.md R5, data-model.md NewStaffUser). The page opens it, reloads on success and shows "Staff user <email> added." Add a page test. T007 green

---

## Phase 4: User Story 3 - An administrator manages roles (P1)

**Independent Test**: spec.md US3.

- [ ] T009 [US3] Write the tests first in `users/roles-dialog.spec.ts`:
  - one switch per role given by the page, in order, with its display name, reflecting the current roles
  - on sends `PUT` and off sends `DELETE` to the encoded role URL
  - a switch is disabled while saving
  - `cannot-remove-own-admin`, `user-disabled` and other failures show their messages and revert the switch
  - closing returns `true` only after a change
  - while a retry is needed (503 `identity-unavailable`), the dialog shows "Sign-in service is unavailable." with "Try again", and the waiting switch's request is re-sent

  See them fail
- [ ] T010 [US3] `users/roles-dialog.ts` per research.md R3 and contracts/ui.md (Roles dialog). The page opens it from "Roles" and reloads when it returns `true`; add a page test. T009 green

---

## Phase 5: User Story 4 - An administrator disables a staff user (P1)

**Independent Test**: spec.md US4.

- [ ] T011 [US4] Write the tests first in `users/users-page.spec.ts`:
  - "Disable" opens the confirmation with the email
  - cancelling sends nothing
  - confirming posts, reloads and shows "<email> disabled."
  - `cannot-disable-self` and other failures show their snack bar messages without a reload

  See them fail, then implement per research.md R4

---

## Phase 6: User Story 5 - Staff see what each role allows (P2)

**Independent Test**: spec.md US5.

- [ ] T012 [US5] Write the tests first in `roles/roles-page.spec.ts`:
  - not-available for the platform admin
  - loading
  - loaded, with role and permission display names from the answer
  - 403 shows "You don't have access to roles."
  - error and "Try again"

  See them fail, then implement `roles/roles-page.ts` per contracts/ui.md (Roles page) and data-model.md (Role)

---

## Phase 7: Browser check, end-to-end, polish

- [ ] T013 Rebuild the Identity and backoffice images with `dev.ps1 up -Profile infra,apps` (never reset); the new migration applies to the existing dev data. Check quickstart.md section 2 in a real browser through the gateway, including a new user with no roles seeing the no-access texts. Capture one real answer per refusal code and compare it with the fixtures
- [ ] T014 `tests/e2e/specs/backoffice-staff.spec.ts` per research.md R7. `scripts/e2e.ps1` passes all four specs; the cleanup check is clean; the dev stack is still running
- [ ] T015 In `NODE_IMAGE` with `CI=true`: lint, unit tests and build. The initial bundle must not grow (the pages are lazy). Review the new files for comments. Run the image pin check
- [ ] T016 [P] Record findings in `specs/019-backoffice-staff/research.md` (R8). Update `README.md` (Users and Roles)

---

## Dependencies

- Phase 0 (T101–T104) comes before T002, which uses the new list answer.
- T001–T004 come first among the backoffice tasks (T004 needs T002 for the routes to resolve).
- US1 comes before US2, US3 and US4, which all live on the Users page. US5 is independent after T002.
- T013 and T014 come after all stories; polish comes last.

## Parallel Examples

T101 and T103's seeding helper; T001, T002 and T003; T012 alongside US2–US4; T016 with T015.
