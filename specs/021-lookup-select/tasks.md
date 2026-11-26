# Tasks: Lookup Select

**Input**: Design documents from `specs/021-lookup-select/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui.md, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**:
- No comments.
- Every Node, npm and ng command runs in `NODE_IMAGE`.
- No option lists in the UI.
- No codes shown in place of names.
- Search runs only on Enter or a button.
- Never reset the dev stack.
- Paths are relative to `src/web/backoffice/src/app/` unless they start with `tests/` or `specs/`.

---

## Phase 1: Foundational

- [ ] T001 `LookupsApi.page(typeCode, request, service?)` in `lookups/lookups-api.ts`, per research.md R6 and data-model.md (LookupPageRequest `q`, `ids`, `codes`, `size`, `after`; LookupValuePage `items`, `nextCursor`).

  Tests first in `lookups/lookups-api.spec.ts`:
  - the URL for the default service and for another service
  - the type code is encoded in the path
  - empty `q` is not sent
  - `ids` and `codes` are sent as repeated parameters
  - `size` and `after` are sent

---

## Phase 2: User Story 1 - Pick one value (P1) 🎯 MVP

**Independent Test**: spec.md US1.

- [ ] T002 [US1] Write the tests first in `lookups/lookup-select.spec.ts`, single mode, with a host component:
  - opening asks for the first page (`size=50`) once, and shows the names in order, with the `emptyLabel` option first when set
  - typing sends nothing
  - Enter with no option highlighted searches with the trimmed text, and so does the search button; an empty text searches without `q`
  - "No matches" shows for an empty answer
  - picking emits the code, or the id when `valueBy="id"`, closes the panel and shows the name
  - picking the empty option emits `null`
  - a preset value shows its name after one `codes=` request; with `valueBy="id"` it uses an `ids=` request
  - a preset value missing from the answer, or a failed names request, shows "Unavailable"
  - a failed page shows "Options could not be loaded." and "Try again", which repeats the request
  - an answer to an older search is ignored
  - Signal Forms: `[formField]` sets and reads the value; `required` shows the form's error under the field; `disabled` cannot be opened; blur marks the field touched
  - `readonly` shows the value but cannot be opened or changed
  - the clear button sets `null`; required fields and fields with `emptyLabel` have none
  - the input has `role="combobox"` and the options `role="option"`; ArrowDown highlights the next option; Escape closes without picking; the search button is labelled `Search <label>`

  See them fail
- [ ] T003 [US1] `lookups/lookup-select.ts` single mode, per research.md R1–R3, R5, R7 and R8 and contracts/ui.md, implementing data-model.md `LookupSelect.typeCode`, `.label`, `.valueBy`, `.emptyLabel`, `.service`, `.value` (single), `.disabled`, `.readonly`, `.required`, `.touched`, `.errors` and `.touch`. T002 green

---

## Phase 3: User Story 2 - Pick several values (P1)

**Independent Test**: spec.md US2.

- [ ] T004 [US2] Write the tests first in `lookup-select.spec.ts`, multiple mode:
  - picks add chips with names, in order, and the value becomes the list of codes
  - the text clears after a pick
  - picking a chosen value again does not duplicate it, and chosen options are marked
  - the chip remove button and Backspace on an empty input remove values
  - preset values show as chips after one `codes=` request
  - an unknown preset shows "Unavailable" and can be removed
  - 250 preset values make two names requests (200 and 50)
  - each chip's remove button is labelled `Remove <name>`
  - `[formField]` binds the list

  See them fail
- [ ] T005 [US2] Multiple mode with `mat-chip-grid` in `lookup-select.ts`, per research.md R2, implementing data-model.md `LookupSelect.multiple` and `.value` (multiple). T004 green

---

## Phase 4: User Story 3 - Long lists (P2)

**Independent Test**: spec.md US3.

- [ ] T006 [US3] Write the tests first in `lookup-select.spec.ts`:
  - scrolling the panel loads the next page with `after` and appends it
  - no request while one is running
  - none after the last page
  - a new search restarts at the first page and drops the old cursor

  See them fail, then implement per research.md R4

---

## Phase 5: Users page (FR-009, FR-009a)

- [ ] T007 Update `users/users-page.spec.ts` first:
  - the Status filter is an `app-lookup-select` over `staff-status` with "All"; picking a status restarts with the total
  - "Search email" sends nothing while typing, and searches with the total on Enter and on its search button
  - empty text searches without `q`
  - the fake-timer test is replaced

  See them fail, then change `users/users-page.ts`: use the component for Status, and make the email search explicit with a search icon button. Remove `searchDelay` and the timer
- [ ] T008 Update `tests/e2e/specs/backoffice-staff.spec.ts`: press Enter after filling "Search email"

---

## Phase 6: Polish

- [ ] T009 In `NODE_IMAGE` with `CI=true`: lint, unit tests and build. Compare the initial bundle with `master` (the component is used only by the lazy Users page). Check new files for comments
- [ ] T010 Rebuild the backoffice with `dev.ps1 up -Profile infra,apps` (never reset) and check quickstart.md section 2. Then `scripts/e2e.ps1`: all specs pass, and the cleanup check is clean
- [ ] T011 Record findings in `specs/021-lookup-select/research.md` (R10). Update `specs/019-backoffice-staff` research R10 and `contracts/ui.md` to point to 021 for the email search. Commit in chunks: docs, component, Users page and e2e. After the owner confirms, push, open the PR and watch CI

---

## Dependencies

- T001 comes before every story.
- US1 (T002–T003) comes before US2 (T004–T005) and US3 (T006), which extend the same component.
- T007 needs T003. T008 follows T007.
- Polish comes last.

## Parallel Examples

T001 alongside T002's test writing; T006 alongside T004 once T003 is done.
