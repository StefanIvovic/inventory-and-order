# Implementation Plan: App-Wide Loading Indicator

**Branch**: `022-loading-indicator` | **Date**: 2026-11-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/022-loading-indicator/spec.md`

## Summary

One thin indeterminate bar under the backoffice toolbar shows while the user waits for the server.
- **Counting** (R1): a root `LoadingService` counts running waits. It shows the bar after 200 ms and hides it when the count falls to zero and stays there to the next task, so back-to-back waits do not flicker (R2).
- **Requests** (R3): a new `loadingInterceptor`, registered before `apiInterceptor`, counts every request. A request held for a sign-in retry still counts. `finalize` ends the wait on success, failure and cancellation.
- **Background** (R4): an `HttpContextToken` marks a request as background. The Users prefetch, the lookup select's option and name pages, the shell's user-type names and the session check use it.
- **Other waits** (R5): `LoadingService.track(promise)` counts any async step. The dialog saves are single requests, so the interceptor covers them (R8).
- **Shell and pages** (R6): the shell renders the bar in a fixed 4 px slot under the toolbar. The Users page removes its reload bar, but keeps `fetching` to disable Previous and Next. No backend change.

## Technical Context

**Language/Version**: TypeScript 6.0, Angular 22.3

**Primary Dependencies**: Angular Material 22.3 (progress-bar), `@angular/common/http` (`HttpContextToken`, functional interceptors). No new packages.

**Storage**: none

**Testing**: Vitest 5 with jsdom and fake timers; `HttpTestingController`; Playwright end-to-end in `e2e-test`

**Target Platform**: evergreen browsers

**Project Type**: web frontend (existing backoffice)

**Performance Goals**:
- no extra requests
- the bar shows only for waits over 200 ms
- no layout shift when it shows

**Constraints**:
- no comments
- the bar means "your action is running": background loads never show it
- pages keep their own first-load states

**Scale/Scope**:
- 4 new files: `loading/loading.service.ts`, `loading/loading.interceptor.ts` and their two specs
- changes to `app.config.ts`, `shell.ts`, `session.service.ts`, `users-api.ts`, `lookups-api.ts`, `lookup-select.ts`, `users-page.ts`, the session test harness, and new tests in the three dialog specs

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Backoffice only; the storefront gets its own copy later (no shared UI library, 016) |
| VI. Authorization | ✅ | No change to what the UI shows or enables; controls still follow the API flags |
| VII. Tests isolated | ✅ | Unit tests with fake timers and `HttpTestingController`; e2e in `e2e-test` |
| VIII. Everything in Docker | ✅ | Tests and build run in the pinned Node image |
| IX. No comments, X. No attribution | ✅ | |
| II, III, IV, V, Lookups | ✅ n/a | No backend, package or lookup change |

**Gate: PASS.** **Post-design re-check**: PASS.

## Project Structure

### Documentation

```text
specs/022-loading-indicator/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/ui.md
└── tasks.md
```

### Source Code

```text
src/web/backoffice/src/app/
├── app.config.ts                      loadingInterceptor before apiInterceptor
├── loading/
│   ├── loading.service.ts             count, 200 ms delay, track(), visible signal
│   ├── loading.service.spec.ts
│   ├── loading.interceptor.ts         BACKGROUND token, background() helper, interceptor
│   └── loading.interceptor.spec.ts
├── shell/shell.ts (+spec)             bar slot under the toolbar
├── session/session.service.ts         session check is background
├── session/testing/session-harness.ts loadingInterceptor before apiInterceptor
├── users/users-api.ts                 list(request, { background })
├── users/users-page.ts (+spec)        prefetch is background; reload bar removed
├── users/add-staff-dialog.spec.ts     save shows the bar (no code change)
├── users/roles-dialog.spec.ts         role switch shows the bar (no code change)
├── tenants/create-tenant-dialog.spec.ts save shows the bar (no code change)
└── lookups/
    ├── lookups-api.ts                 values/page take { background }
    └── lookup-select.ts               option and name pages are background
```

**Structure Decision**: a new `loading/` folder, like `session/`, holds the service and its interceptor.

## Complexity Tracking

No deviations.
