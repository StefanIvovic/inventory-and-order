# Research: App-Wide Loading Indicator

Read on 2026-11-26 from the installed packages: Angular 22.3 (`@angular/common/http`: `HttpContextToken`, `HttpContext`, functional interceptors), Angular Material 22.3 (`mat-progress-bar`), Vitest 5 with jsdom 31 and fake timers. The current backoffice code is from 021.

## R1. Counting waits

- **Decision**: a root `LoadingService` holds a count of running waits.
  - `begin()` adds one wait and returns an `end` function. `end` runs only once, so calling it twice cannot make the count negative.
  - `track(work)` calls `begin()`, awaits `work` and ends the wait in `finally`. It returns the same result or error as `work`.
  - `visible` is a read-only signal that the shell binds to.
- **Why**: one counter can serve both requests and other async steps (FR-003, FR-005). Signals fit the zoneless app.
- **Rejected**: a signal per page. That is the in-page bar this feature removes, and it cannot cover dialogs.

## R2. Delay and no flicker

- **Decision**:
  - when a wait begins and the bar is hidden, a 200 ms show timer starts, unless one is already running
  - when the count falls to zero, a hide check is scheduled for the next task (`setTimeout(0)`)
  - if a new wait begins before the check runs, the check is cancelled, the bar stays, and a running show timer keeps going
  - when the check runs with the count still at zero, the show timer is cleared and the bar hides
  - the 200 ms value is one exported constant, `loadingDelay`
- **Why**: a request that finishes within 200 ms never shows the bar (FR-002). A wait that ends and another that starts in the same task, such as a dialog save followed by the page's reload, keep one continuous bar.
- **Rejected**:
  - a minimum display time, such as 400 ms. The spec does not ask for it, and it would keep the bar after the work is done.
  - RxJS `debounceTime` over a count stream. Plain timers are easier to drive with fake timers.

## R3. Requests: `loadingInterceptor`

- **Decision**:
  - a functional interceptor runs for every request, `/api/` and `/bff/`, unless it is marked background
  - it is registered first, before `apiInterceptor`
  - the wait begins on subscribe (`defer`) and ends in `finalize`
- **Why**:
  - `finalize` runs on success, error and unsubscribe, so cancelled requests (`switchMap`, a component destroyed) end their wait (FR-003, SC-003)
  - because it is outermost, a request that `apiInterceptor` holds for a sign-in retry still counts (spec edge case)
- **Rejected**: listening to `HttpEventType.Sent` and `Response`. It would miss cancellation.

## R4. Background requests

- **Decision**:
  - `loading.interceptor.ts` exports `BACKGROUND = new HttpContextToken<boolean>(() => false)` and a `background()` helper that returns an `HttpContext` with it set
  - the API services take an optional last argument, `RequestOptions = { background?: boolean }`, and set the context from it:
    - `UsersApi.list(request, options)`
    - `LookupsApi.values(typeCode, options)`
    - `LookupsApi.page(typeCode, request, service, options)`
  - marked as background (FR-004):
    - the Users page's `prefetch`
    - every request of `LookupSelect`: option pages and names
    - the shell's `user-type` names
    - `SessionService.load()`, which calls `http.get` directly with `{ context: background() }`
- **Why**: `HttpContextToken` is Angular's documented way to pass per-request metadata to interceptors. It adds no header and does not reach the server.
- **Rejected**:
  - a URL pattern list. That is fragile, and the same URL is sometimes a user action (the first Users page) and sometimes background (the prefetch).
  - a custom header that is stripped again. It is more code and could leak.

## R5. Waits that are not one request

- **Decision**:
  - `LoadingService.track(...)` is there for any async step that is not one request
  - the Add staff user and Create tenant saves are single requests, so the interceptor covers them; see R8 for why they do not use `track`
  - the Roles dialog's switches are single requests, which the interceptor already counts, so the dialog is unchanged
- **Why**: FR-005 and User Story 3. The request inside the tracked step also counts, and the waits overlap, so there is no flicker.

## R6. The bar in the shell, and the pages

- **Decision**:
  - **Slot**: a 4 px `.loading` slot sits directly under `mat-toolbar` and above the retry banner and the side navigation. It always takes its space, so the content does not jump.
  - **Bar**: while `visible()` is true, the slot holds `<mat-progress-bar mode="indeterminate" aria-label="Loading">`.
  - **Announcement**: when `visible` turns true, the shell calls CDK `LiveAnnouncer.announce('Loading', 'polite')`, so screen readers hear it once (FR-006). See R8 for why it is not a status element in the shell.
  - **Users page**: the `@if (fetching())` reload bar is removed. `fetching` stays, because it disables Previous and Next during a page change (FR-007).
  - **Other pages**: first-load bars ("Loading users", "Loading roles", "Loading tenants") and the lookup panel's "Loading options" row are unchanged.
- **Why**: the shell exists only while signed in, so the bar does too (spec edge case). A `progressbar` with a label is exposed to screen readers, but it is not announced when it appears; a polite status region is.
- **Rejected**: a fixed overlay at the top of the viewport. It would cover the toolbar.

## R7. Tests

- **`LoadingService`** (fake timers):
  - hidden before 200 ms and shown at 200 ms
  - a wait under 200 ms never shows the bar
  - overlapping waits keep it until the last one ends
  - end then begin in the same task does not hide it
  - `end` twice is safe
  - `track` resolves and rejects with the work's result and ends the wait either way
- **`loadingInterceptor`** (`HttpTestingController`, fake timers):
  - success, error and unsubscribe all end the wait
  - a background request never counts
  - a background request and a user request together: the bar hides when the user request ends
  - a 503 `identity-unavailable` held for retry keeps the wait until it is answered
- **Users page**:
  - the prefetch request carries `BACKGROUND`
  - no reload bar after a filter change
  - Previous and Next are still disabled while fetching
- **Lookup select**: its requests carry `BACKGROUND`.
- **Shell**: the slot shows the bar while `visible` is true, and is empty while it is false; "Loading" is announced once each time the bar shows.
- **Session**: `load()` is background.
- **Dialogs**: a save shows `visible` after 200 ms while the request is open and hides after the dialog closes.
- **End-to-end**: the specs stay as they are and must still pass (SC-004).

## R8. Findings during implementation (2026-11-26)

- **No `track` in the dialogs**:
  - the interceptor ends a wait when the answer arrives, and the bar hides only on the next task
  - the dialog closes, or shows its errors, in the microtasks right after the answer, before that task
  - so wrapping `submit(...)` in `track(...)` would change nothing anyone can see
  - the dialog tests prove the bar shows while saving and hides after the dialog closes or shows the error, with no dialog code changed
  - `track` stays in `LoadingService` and is unit tested, for future steps that chain several requests
- **`LiveAnnouncer` instead of a status element**:
  - `.cdk-visually-hidden` is styled only when a CDK component loads those styles on demand; `mat.theme` does not include them
  - a hand-made hidden status `span` could therefore show on the page
  - `LiveAnnouncer` brings its own hidden live region
  - it also keeps the shell's only `role="status"` as the sign-in retry banner, which the existing tests look for
- **Fake timers in tests**:
  - switch them on before the session harness loads; otherwise a real show timer from the session request is left pending
  - the session request is now background, so this only matters for tests that create their own requests

