---

description: "Task list for feature 016 Backoffice Shell"
---

# Tasks: Backoffice Shell

**Input**: Design documents from `specs/016-web-backoffice-shell/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**:
- No comments in any file, generated files included.
- Every Node, npm and ng command runs inside `NODE_IMAGE` from `deploy/versions.env`; nothing is installed on the host.
- No CDN or external font.
- End-to-end runs never touch the dev stack.
- Paths are relative to `src/web/backoffice/` unless they start with `src/`, `deploy/`, `scripts/`, `tests/` or `.github/`.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Add `NODE_IMAGE=node:26.13.1-alpine3.24`, `NGINX_IMAGE=nginxinc/nginx-unprivileged:1.30.6-alpine3.24` and `PLAYWRIGHT_IMAGE=mcr.microsoft.com/playwright:v1.65.1-noble` to `deploy/versions.env` (research.md R1). Add `node_modules`, `dist`, `.angular`, `tests/e2e/test-results` and `tests/e2e/playwright-report` to `.gitignore`
- [X] T002 Generate the app in `NODE_IMAGE` with the `ng new` command in research.md R2, then `ng add @angular/material` and `ng add angular-eslint@22.8.0`.
  - Pin TypeScript to the latest 6.0.x.
  - Set `baseHref: "/backoffice/"` in the build options.
  - Check that `app.config.ts` has no zone change detection and that `zone.js` is absent.
  - Remove every generated comment and the sample spec.
  - `npm ci`, `ng lint`, `ng test --no-watch --no-progress` and `ng build` pass.
- [X] T003 [P] `nginx.conf` per contracts/serving.md and research.md R3; `Dockerfile` (build arguments `NODE_IMAGE` and `NGINX_IMAGE` without defaults, `npm ci` plus `ng build`, copy to `/usr/share/nginx/html/backoffice/`); `.dockerignore`
- [X] T004 [P] `compose.yaml`: service `backoffice-spa`, profile `apps`, build arguments from `versions.env`, `wget` health check on `/health`, `restart: on-failure`, no published port. Include it in `deploy/compose.yaml`. Change the gateway's `backoffice-spa` address to port 8080 in `src/gateway/Gateway.Api/appsettings.json` and `src/gateway/compose.yaml` (FR-012); gateway tests stay green

---

## Phase 2: Foundational

- [X] T005 `session/browser-location.ts`: an injectable `BROWSER_LOCATION` with `path()`, `search()`, `assign(url)` and `replaceSearch(search)`, defaulting to `window.location` and `history`. Also a test fake in `session/testing/fake-browser-location.ts`
- [X] T006 [P] `session/sign-in-errors.ts`: code to message per contracts/ui.md. Unit test in `session/sign-in-errors.spec.ts` for the three codes and an unknown one
- [X] T007 `session/session.service.ts` with the states and transitions in data-model.md (Session state, Session view `userType` and `logoutUrl`). Write the tests first in `session/session.service.spec.ts`, using `provideHttpClient()`, `provideHttpClientTesting()` and the fake location:
  - 200, 401, 500 and a network error lead to the right state
  - `signIn()` assigns the login URL with the current path and query, without `signin-error`
  - `signOut()` assigns `logoutUrl` as received
  - `signin-error` is read once and removed from the address

  See them fail, then implement

---

## Phase 3: User Story 1 - Sign in and see the backoffice (P1) 🎯 MVP

**Goal**: sign-in screen without a session; the frame with email and role with a session; sign-in errors shown.

**Independent Test**: spec.md US1.

- [X] T008 [US1] Write the component tests first, in `app.spec.ts`, `sign-in/sign-in.spec.ts` and `shell/shell.spec.ts`, with the session state set through the service:
  - loading shows only the progress bar
  - signed-out shows the "Sign in" button and the error line
  - signed-in shows the email and "Staff" or "Platform administrator", and no sign-in button
  - unavailable shows "Try again", which calls `load()` again
  - the button calls `signIn()`

  See them fail
- [X] T009 [US1] `app.ts` and its template render by state; `sign-in/`, `unavailable/`, `shell/` (Material toolbar, role chip, user menu, outlet) and `home/` placeholder per contracts/ui.md; `app.config.ts` loads the session at start with `provideAppInitializer(() => inject(SessionService).load())`, and the router has `''` to home plus `'**'` to home. No icon font: the user menu uses a text button or an inline SVG icon registered with `MatIconRegistry` (research.md R3). T008 green
- [X] T010 [US1] Check in the dev stack: build the image, run `dev.ps1 up -Profile infra,apps`, open `/backoffice/orders` in a browser, sign in as the demo admin, and land back on `/backoffice/orders` with the frame. A deep link reload serves the app

---

## Phase 4: User Story 2 - Sign out (P1)

**Goal**: "Sign out" uses the session's sign-out address and ends on the sign-in screen.

**Independent Test**: spec.md US2.

- [X] T011 [US2] Write the test first in `shell/shell.spec.ts`: "Sign out" in the menu calls `signOut()`, which assigns the session's `logoutUrl`. See it fail, then wire the menu item

---

## Phase 5: User Story 3 - API calls and an ended session (P1)

**Goal**: `/api/**` calls carry the app header; 401 leads to sign-in; 503 waits for "Try again".

**Independent Test**: spec.md US3.

- [X] T012 [US3] Write the tests first in `session/api.interceptor.spec.ts`, using `provideHttpClient(withInterceptors([apiInterceptor]))` and `provideHttpClientTesting()`, in that order:
  - `/api/identity/users` carries `X-Bff-App: backoffice`, and `/backoffice/x.json` and `/bff/backoffice/user` do not
  - a 401 problem moves the state to signed-out with the router URL as the return path, and the caller sees the error
  - a 503 `identity-unavailable` sets `retryNeeded`, and nothing reaches the caller until `retry()`; after that the same request is sent again and its answer reaches the caller
  - other statuses pass through

  See them fail
- [X] T013 [US3] `session/api.interceptor.ts` per research.md R4 and contracts/ui.md HTTP rules; register it in `app.config.ts`; add the retry banner to the shell. T012 green

---

## Phase 6: User Story 4 - Own build, CI and Docker dev mode (P2)

**Goal**: the image serves correctly; dev mode reloads live through the gateway; its own CI workflow.

**Independent Test**: spec.md US4.

- [X] T014 [US4] `deploy/compose.web-dev.yaml` per research.md R5. Add `-WebDev` to `scripts/dev.ps1`: it adds the file to every command, and the usage text is updated
- [X] T015 [US4] Check dev mode: `dev.ps1 up -Profile infra,apps -WebDev`, change the home text, and the page updates within 5 seconds (SC-004). Sign-in works. If the hot-reload socket does not pass through the gateway, fix it (research.md R5 *verify*) and record the result in research.md
- [X] T016 [US4] `.github/workflows/backoffice.yml` per research.md R9, including the `docker exec` smoke checks: host page on `/backoffice/` and on a deep link, `no-cache` on the host page, `immutable` on a hashed `main-*.js`, non-root user, `/health` 200, and the security headers
- [X] T017 [US4] Add the `/backoffice/` host-page check to `scripts/smoke-signin.ps1`, and confirm `deploy.yml` still passes locally (full stack plus smoke)

---

## Phase 7: User Story 5 - First end-to-end test (P2)

**Goal**: a Playwright sign-in test on a throwaway `e2e-test` stack that always cleans up.

**Independent Test**: spec.md US5.

- [X] T018 [US5] `tests/e2e/package.json` (`@playwright/test` 1.65.1) with its lockfile generated in `PLAYWRIGHT_IMAGE`, plus `tsconfig.json` and `playwright.config.ts` per research.md R7. Check that the `trace` value is accepted
- [X] T019 [US5] `tests/e2e/compose.e2e.yaml` per contracts/serving.md (End-to-end) and research.md R7: ports reset, volumes reset plus `tmpfs`, and the `playwright` service in the gateway's network namespace with `env_file: [../../deploy/dev.env]` for the demo admin credentials
- [X] T020 [US5] `tests/e2e/specs/backoffice-sign-in.spec.ts` (FR-016):
  - open `/backoffice/orders`, see "Sign in", and click it
  - fill Identity's form with `DEMO_ADMIN_EMAIL` and `DEMO_ADMIN_PASSWORD`, passed in as environment variables
  - land on `/backoffice/orders` and see the email and "Staff"
  - sign out and see "Sign in"
- [X] T021 [US5] `scripts/e2e.ps1` per research.md R7: `restore-feed.ps1 -Service identity`, then `up --build --detach --wait`, `run --rm playwright`, and in `finally` `down -v --remove-orphans` plus `check-test-cleanup.ps1 -Project e2e-test`, exiting with the test's code. Run it while the dev stack is up: it passes, and the dev stack is unchanged (SC-003). Run it once with a deliberately failing assertion: the stack is still torn down and the trace is written
- [X] T022 [US5] `.github/workflows/e2e.yml` per research.md R7: `scripts/e2e.ps1`; upload `tests/e2e/test-results` when it fails; then two `if: always()` steps, `docker compose -p e2e-test down -v --remove-orphans` and `check-test-cleanup.ps1 -Project e2e-test` (constitution VII)

---

## Phase 8: Polish

- [X] T023 Run quickstart.md sections 1–5. Review every new file for comments (generated configs especially) and attribution. Run `dotnet run --project tools/RepoChecks -- images` and `-- packages`. Run the gateway tests
- [X] T024 [P] Update `README.md` and `deploy/README.md`: backoffice URL, `-WebDev`, `scripts/e2e.ps1`
- [X] T025 [P] Record implementation findings in `specs/016-web-backoffice-shell/research.md` (R11)

---

## Dependencies

- **Setup:** T001 → T002 → T003 and T004.
- **Foundational:** T005 → T006 and T007 (in parallel).
- **Stories:**
  - US1 needs the Foundational phase.
  - US2 and US3 need US1's shell.
  - US4 needs T003 and T004.
  - US5 needs US1, US2 and T004, because the end-to-end test signs in and out against the image.
- **Polish** comes last.

## Parallel Examples

- T003 with T004.
- T006 with T007.
- T014 alongside US3.
- T018 with T019.
- T024 with T025.

## Implementation Strategy

1. Setup, Foundational and US1 make the MVP: sign-in and the frame work in the dev stack.
2. Add US2 and US3, which complete the session module.
3. Add US4 for dev mode and CI, then US5 for the end-to-end test.
