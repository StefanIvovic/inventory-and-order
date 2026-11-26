# Tasks: App-Wide Loading Indicator

**Input**: Design documents from `specs/022-loading-indicator/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui.md, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**:
- No comments.
- Every Node, npm and ng command runs in `NODE_IMAGE`.
- Never reset the dev stack.
- Paths are relative to `src/web/backoffice/src/app/` unless they start with `tests/` or `specs/`.

---

## Phase 1: Foundational

- [X] T001 `loading/loading.service.ts`, per research.md R1 and R2, implementing data-model.md `LoadingService.visible`, `.begin()`, `.track(work)` and `.loadingDelay`, with its States and Transitions.

  Tests first in `loading/loading.service.spec.ts`, with fake timers:
  - hidden at 199 ms, visible at 200 ms
  - a wait that ends before 200 ms never shows the bar, even after the timers run on
  - two overlapping waits keep the bar until the second ends, then it hides after the next task
  - `end` then `begin` in the same task keeps the bar visible
  - calling `end` twice does not end another wait
  - `track` resolves with the work's value and rejects with its error, and ends the wait either way

  See them fail, then implement

- [X] T002 `loading/loading.interceptor.ts`, per research.md R3 and R4, implementing data-model.md `BACKGROUND`, `background()` and `RequestOptions`.

  Tests first in `loading/loading.interceptor.spec.ts`, with `provideHttpClient(withInterceptors([loadingInterceptor, apiInterceptor]))`, `HttpTestingController` and fake timers:
  - a slow request shows `visible`; its answer, an error answer and an unsubscribe each end the wait
  - a request with `context: background()` never counts
  - with a background request and a user request open, the bar hides when the user request ends
  - a 503 `identity-unavailable` held for retry keeps the bar until the retried request is answered

  See them fail, then implement. Register `loadingInterceptor` before `apiInterceptor` in `app.config.ts` and in `session/testing/session-harness.ts`, so page and dialog tests use the same interceptors as the app

---

## Phase 2: User Story 1 - See that the app is working (P1) 🎯 MVP

**Independent Test**: spec.md US1.

- [X] T003 [US1] Shell bar, per research.md R6 and contracts/ui.md (Shell). Tests first in `shell/shell.spec.ts`, driving the bar with `LoadingService.begin()`, its `end` function and fake timers advanced by `loadingDelay`:
  - while `visible` is false: the `.loading` slot is present and empty, and the status text is empty
  - while `visible` is true: one `mat-progress-bar` with `aria-label="Loading"` is in the slot, and the `role="status"` text is "Loading"
  - the slot comes directly after `mat-toolbar` and before the retry banner

  See them fail, then change `shell/shell.ts`: the 4 px slot, the bar and the visually hidden status
- [X] T004 [US1] Users page reload bar, per research.md R6. Tests first in `users/users-page.spec.ts`:
  - after a filter change, no `mat-progress-bar` is shown while the list loads
  - Previous and Next are still disabled while a page loads
  - the first-load bar stays

  See them fail, then remove the `@if (fetching())` bar from `users/users-page.ts`

---

## Phase 3: User Story 2 - Background loads stay quiet (P1)

**Independent Test**: spec.md US2.

- [X] T005 [P] [US2] `UsersApi.list(request, options)` in `users/users-api.ts`, implementing data-model.md `RequestOptions`. Tests first in `users/users-api.spec.ts`: `{ background: true }` sets `BACKGROUND`, and no options leave it false
- [X] T006 [P] [US2] `LookupsApi.values(typeCode, options)` and `LookupsApi.page(typeCode, request, service, options)` in `lookups/lookups-api.ts`, implementing data-model.md `RequestOptions`. Tests first in `lookups/lookups-api.spec.ts`, the same two cases for each
- [X] T007 [US2] Mark the background sources from the data-model.md Wait sources table. Tests first:
  - `users/users-page.spec.ts`: the prefetch request carries `BACKGROUND`, and the first page and a filter change do not
  - `lookups/lookup-select.spec.ts`: option pages and names requests carry `BACKGROUND`
  - `shell/shell.spec.ts`: the `user-type` request carries `BACKGROUND`
  - `session/session.service.spec.ts`: the `/bff/backoffice/user` request carries `BACKGROUND`

  See them fail, then change `users/users-page.ts` (`prefetch`), `lookups/lookup-select.ts`, `shell/shell.ts` and `session/session.service.ts`. Depends on T005 and T006

---

## Phase 4: User Story 3 - Waits that are not a single request (P2)

**Independent Test**: spec.md US3.

- [X] T008 [P] [US3] Add staff user save, per research.md R5 and R8. Test in `users/add-staff-dialog.spec.ts`: while the create request is open, after 200 ms, `LoadingService.visible` is true. After a success it is false once the dialog has closed. After a 409 it is false and the error shows. No code change: the interceptor already covers the save (R8)
- [X] T009 [P] [US3] The same for Create tenant, in `tenants/create-tenant-dialog.spec.ts`. No code change (R8)
- [X] T010 [P] [US3] Roles dialog, per research.md R5: no code change. Test in `users/roles-dialog.spec.ts`, with fake timers: a role switch whose request is still open shows `LoadingService.visible` after 200 ms, and it is false after the answer, and after an error answer. Depends on T002 (the harness has `loadingInterceptor`)

---

## Phase 5: Polish

- [X] T011 In `NODE_IMAGE` with `CI=true`: lint, unit tests and build. Check new and changed files for comments
- [X] T012 Rebuild the backoffice with `dev.ps1 up -Profile infra,apps` (never reset) and check quickstart.md section 2. Then `scripts/e2e.ps1`: all specs pass (SC-004), and the cleanup check is clean
- [X] T013 Record findings in `specs/022-loading-indicator/research.md` (R8). Commit in chunks: docs, loading service and interceptor, shell and pages. After the owner confirms, push, open the PR and watch CI

---

## Dependencies

- T001 comes before T002, and both come before every story.
- US1 (T003–T004) and US2 (T005–T007) are independent of each other. US3 needs T001 for T008–T009 and T002 for T010.
- T007 needs T005 and T006.
- Polish comes last.

## Parallel Examples

- T005 and T006 together.
- T008, T009 and T010 together.
- T003 alongside T005 and T006, since they touch different files.

## Implementation Strategy

MVP is Phase 1 plus US1. The bar then shows for every request, background loads included. US2 makes it quiet for background loads, and US3 covers dialog saves end to end and proves the Roles dialog. Ship all three in one PR.
