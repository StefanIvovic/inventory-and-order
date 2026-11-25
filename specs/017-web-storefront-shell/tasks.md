---

description: "Task list for feature 017 Storefront Shell"
---

# Tasks: Storefront Shell

**Input**: Design documents from `specs/017-web-storefront-shell/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**: the same as 016.
- No comments in any file.
- Every Node, npm and ng command runs in `NODE_IMAGE`.
- No CDN.
- No code imported from the backoffice: copies only.
- End-to-end runs never touch the dev stack.
- Paths are relative to `src/web/storefront/` unless they start with `src/`, `deploy/`, `scripts/`, `tests/` or `.github/`.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Generate the app in `NODE_IMAGE` per research.md R1:
  - mount `src/web` at `/work` in `NODE_IMAGE` and run `ng new storefront --directory storefront` there, with the 016 flags, then `ng add @angular/material@22.3.0` and `ng add angular-eslint@22.8.0`
  - set `<base href="/shop/">` and drop Material's Google Fonts links
  - use the `system-ui` theme typography and turn off `inlineCritical` in production
  - remove `.vscode`, `README.md`, the project `.gitignore`, every generated comment, the sample `app.html`, `app.scss` and `app.spec.ts`
  - `ng lint`, `ng test` and `ng build` pass
- [X] T002 [P] `nginx.conf` and `security-headers.conf` per contracts/serving.md and research.md R4 (IPv4 and IPv6). `Dockerfile` and `.dockerignore` copied from the backoffice and adapted to `dist/storefront` and `/usr/share/nginx/html/shop`
- [X] T003 [P] `compose.yaml` (`storefront-spa`, profile `apps`, `wget` health check on `/health`, no port); include it in `deploy/compose.yaml`. Change the gateway's `storefront-spa` address to `:8080` in `src/gateway/Gateway.Api/appsettings.json` and `src/gateway/compose.yaml`; gateway tests stay green

---

## Phase 2: Foundational

- [X] T004 [P] `app/shop.ts`: `SHOP_SLUG_PATTERN`, `isShopSlug`, `shopSlugFromPath(path)` and `shopMatcher` (data-model.md, Shop identifier). Write the tests first in `app/shop.spec.ts`: the accepted and rejected examples from the gateway's `SlugTests`, path parsing for `/shop/demo/x`, `/shop/` and `/shop/Demo/`, and the matcher consuming exactly one valid segment
- [X] T005 Copy `session/browser-location.ts` and `session/testing/fake-browser-location.ts` from the backoffice. Write `session/sign-in-errors.ts` with the storefront wording from contracts/ui.md, with its spec first
- [X] T006 `session/session.service.ts` with the states and transitions in data-model.md (Session state, Session view `slug` and `logoutUrl`). Write the tests first in `session/session.service.spec.ts`:
  - an invalid slug gives `no-shop` and makes no request
  - 200 gives `signed-in`, 401 gives `visitor`, and 500 or a network error gives `unavailable`
  - `signIn()` goes to `/bff/shop/<slug>/login` with the current path and query, without `signin-error`
  - `signOut()` uses `logoutUrl`
  - `signin-error` is read once and removed from the address

  See them fail, then implement
- [X] T007 `session/api.interceptor.ts` (`X-Bff-App: storefront`; a 401 gives `visitor`; a 503 `identity-unavailable` waits for retry). Write the tests first in `session/api.interceptor.spec.ts`, mirroring the backoffice ones with the storefront header and the `visitor` state. Also add `session/testing/session-harness.ts`

---

## Phase 3: User Story 1 - A visitor opens a shop (P1) 🎯 MVP

**Goal**: shop frame for visitors, "shop not found" for bad addresses.

**Independent Test**: spec.md US1.

- [X] T008 [US1] Write the tests first in `app.spec.ts` and `frame/frame.spec.ts`:
  - loading shows only the progress bar
  - `visitor` shows the title "demo", the home text and "Sign in"
  - `unavailable` shows the frame with the trouble banner, and "Try again" calls `load()` again
  - `no-shop` shows "This shop doesn't exist." with no "Sign in"
  - the routes send `/shop/`-style URLs to "shop not found"

  See them fail
- [X] T009 [US1] `app.ts`, `frame/`, `home/`, `shop-not-found/`, `app.routes.ts` (research.md R2) and `app.config.ts` (`provideAppInitializer` load, interceptor). No icon font. T008 green
- [X] T010 [US1] Check in a browser on the dev stack: `/shop/demo/` and `/shop/demo/x` show the frame; `/shop/` and `/shop/Demo/` show "shop not found"; there are no CSP errors in the console

---

## Phase 4: User Story 2 - Register or sign in, and see the account (P1)

**Goal**: sign-in and registration through Identity, the account menu and the Account page.

**Independent Test**: spec.md US2.

- [X] T011 [US2] Write the tests first in `frame/frame.spec.ts` and `account/account.spec.ts`:
  - "Sign in" assigns the shop login URL
  - signed in, the account menu shows the email (or user id), with "Account" and "Sign out"
  - the account page shows the email and the slug when signed in, and the sign-in prompt otherwise
  - the sign-in error line shows the storefront message

  See them fail
- [X] T012 [US2] `account/` page and the frame's account menu per contracts/ui.md. T011 green
- [X] T013 [US2] Check in a browser: register a new customer on `/shop/demo/` through "Create an account" and land back signed in. "Account" shows the email and "demo". A session for `demo` shows as a visitor on another well-formed shop. `?signin-error=access_denied` shows its message and is removed from the address

---

## Phase 5: User Story 3 - Sign out (P1)

**Goal**: sign-out through Identity's confirmation, landing as a visitor.

**Independent Test**: spec.md US3.

- [X] T014 [US3] Write the test first in `frame/frame.spec.ts`: "Sign out" in the account menu assigns `logoutUrl`. See it fail, then wire it. In the browser, sign out, confirm, and land on `/shop/demo/` as a visitor

---

## Phase 6: User Story 4 - API calls and an ended session (P1)

**Goal**: 401 leads to the visitor view; 503 shows the retry banner.

**Independent Test**: spec.md US4.

- [X] T015 [US4] Write the test first in `frame/frame.spec.ts`: after a 503 `identity-unavailable` the banner shows, and "Try again" re-sends the request and hides the banner. A 401 from an API call switches the frame to the visitor view. See it fail, then add the banner. T007 covers the interceptor itself

---

## Phase 7: User Story 5 - Build, CI, live reload, end-to-end (P2)

**Goal**: own CI, live reload for both apps, the end-to-end registration test.

**Independent Test**: spec.md US5.

- [X] T016 [US5] `deploy/compose.web-dev.yaml`: add `storefront-dev` and the gateway override (research.md R5). Check that both apps reload within 5 seconds with `dev.ps1 up -Profile infra,apps -WebDev`
- [X] T017 [US5] `.github/workflows/storefront.yml` per research.md R6. Run its smoke checks locally against the image first
- [X] T018 [US5] `scripts/smoke-signin.ps1`: add the `/shop/demo/` host-page check (`<base href="/shop/"`). The full smoke passes
- [X] T019 [US5] `tests/e2e/specs/storefront-register.spec.ts` per research.md R7, plus one step before sign-out: open another well-formed shop (`/shop/other-shop/`) and see "Sign in" (SC-006). `scripts/e2e.ps1` runs both specs and passes while the dev stack is up; the cleanup check is clean

---

## Phase 8: Polish

- [X] T020 Run quickstart.md sections 1–5. Review all new files for comments. Run the image and package pin checks and the gateway tests
- [X] T021 [P] Update `README.md` and `deploy/README.md`: the storefront URL, and `-WebDev` covering both apps
- [X] T022 [P] Record findings in research.md (R8)

---

## Dependencies

- **Setup and Foundational:** T001 → T002 and T003. T004–T007 depend on T001; T006 needs T004 and T005; T007 needs T006.
- **Stories:** US1 needs the Foundational phase. US2, US3 and US4 need US1's frame. US5 needs T002, T003 and US2–US3 (the end-to-end test registers and signs out).
- **Polish** comes last.

## Parallel Examples

- T002 with T003.
- T004 with T005.
- T017 with T018.
- T021 with T022.

## Implementation Strategy

1. Setup, Foundational and US1 make the MVP: the frame for visitors and "shop not found".
2. Add US2 and US3 (account, sign-out), then US4 (banner).
3. Add US5 for CI, live reload and the end-to-end test.
