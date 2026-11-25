# Feature Specification: Backoffice Shell

**Feature Branch**: `016-web-backoffice-shell`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "016 web-backoffice-shell: the backoffice Angular SPA shell in src/web/backoffice, with its own Dockerfile, compose file and CI workflow. Angular 22.3 with Angular Material 22.3, standalone components, Vitest unit tests, served by nginx-unprivileged 1.30.6-alpine3.24 on port 8080 at base href /backoffice/ with index.html fallback; built with node 26.13.1-alpine3.24; images pinned in deploy/versions.env. The gateway's backoffice-spa cluster moves from :80 to :8080. Auth through the gateway BFF only (no OIDC library): on start GET /bff/backoffice/user; 401 shows a sign-in screen whose button navigates to /bff/backoffice/login?returnUrl=<current path>; signed in shows a shell (toolbar with app name, user email and user type, sign-out menu using logoutUrl); ?signin-error= shows a friendly message; an HTTP interceptor adds X-Bff-App: backoffice to every /api/** call; a 401 from the API sends the user back to sign-in with the current path as returnUrl; 503 identity-unavailable shows a retry message. No business screens yet. The BFF client is self-contained in the app, not shared with the storefront. Development runs in Docker too: profile apps serves the built nginx image; a separate dev profile runs the Angular dev server in a node container with the source mounted and hot reload, and the gateway's backoffice-spa cluster points at it through a dev override. Tests: unit tests for the BFF client, interceptor and guards; the first Playwright end-to-end test in tests/e2e running in the compose project e2e-test, always torn down, signing in as the demo admin through the gateway and seeing the shell, then signing out. English only, plain Angular Material theme."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A staff member signs in and sees the backoffice (Priority: P1)

A staff member opens `http://localhost:8080/backoffice/`. They are not signed in, so the backoffice shows a sign-in screen with one button. The button takes them to Identity's sign-in page. After signing in they come back to the page they started on, and the backoffice shows its frame: the app name, their email and their role (staff or platform administrator), and a user menu.

**Why this priority**: Every backoffice screen in later features needs this frame and a signed-in user. Until now sign-in worked only by typing gateway addresses.

**Independent Test**: With the stack running, open `/backoffice/some/page`, press "Sign in", sign in as the demo tenant admin, and land back on `/backoffice/some/page` with the frame showing the admin's email and "Staff".

**Acceptance Scenarios**:

1. **Given** no session, **When** any backoffice page is opened, **Then** a sign-in screen is shown instead of the frame, with no flash of signed-in content.
2. **Given** the sign-in screen at `/backoffice/a/b`, **When** the user presses "Sign in", **Then** the browser goes to the gateway's backoffice login with `returnUrl=/backoffice/a/b`, and after sign-in it lands on `/backoffice/a/b`.
3. **Given** a session, **When** a backoffice page is opened or reloaded, **Then** the frame shows the user's email and "Staff" or "Platform administrator".
4. **Given** sign-in failed or was refused at Identity, **When** the backoffice opens with `?signin-error=<code>`, **Then** it shows the plain message for that code from contracts/ui.md above the sign-in button, and removes the code from the address bar.
5. **Given** the user directly opens a deep link like `/backoffice/settings/x`, **When** the page is served, **Then** the backoffice app loads (not a "not found" page from the web server).

---

### User Story 2 - A staff member signs out (Priority: P1)

From the user menu the staff member chooses "Sign out". They are signed out at the gateway and at Identity and land on the backoffice sign-in screen.

**Why this priority**: Signing out is part of the minimum for any shared computer, and it must use the sign-out proof the gateway requires.

**Independent Test**: Signed in, choose "Sign out": the browser ends on `/backoffice/` showing the sign-in screen, and pressing "Sign in" asks for the password again.

**Acceptance Scenarios**:

1. **Given** a session, **When** the user chooses "Sign out", **Then** the browser goes to the sign-out address the gateway gave with the session (including its proof), and ends on the backoffice sign-in screen.
2. **Given** the user has signed out, **When** they press "Sign in", **Then** Identity asks for their password again.

---

### User Story 3 - Screens call APIs through the session, and an ended session is handled (Priority: P1)

Backoffice screens call service APIs through the gateway. Every call names the app so the gateway uses the backoffice session. When the session has ended (signed out elsewhere, disabled, expired), the next call sends the user back to the sign-in screen and returns them to where they were afterwards. When Identity is briefly unavailable, the user sees a message and can retry.

**Why this priority**: Feature 018 builds the first real screens on this. Getting session handling right once, here, keeps every later screen simple.

**Independent Test**: In unit tests with a fake HTTP backend: an `/api/...` call carries the app header; a 401 problem leads to the sign-in screen with the current page as return path; a 503 `identity-unavailable` shows a retry message and a retry repeats the call.

**Acceptance Scenarios**:

1. **Given** any call to `/api/**`, **When** it is sent, **Then** it carries the backoffice app header and the browser's same-origin cookies, and nothing else is added.
2. **Given** a call answers 401 (`session-required` or `session-ended`), **When** the answer arrives, **Then** the user sees the sign-in screen, and signing in returns them to the page they were on.
3. **Given** a call answers 503 `identity-unavailable`, **When** the answer arrives, **Then** the user sees "Sign-in service is unavailable, try again" with a retry action, and the session is kept.
4. **Given** calls to anything other than `/api/**` (for example the app's own files), **When** they are sent, **Then** they carry no app header.

---

### User Story 4 - The backoffice is built and shipped on its own, and runs in Docker during development (Priority: P2)

The backoffice lives in its own folder with its own build, image, compose file and CI workflow. Developers run it in Docker: the normal stack serves the built app; a development mode serves it with live reload, still through the gateway at `localhost:8080`.

**Why this priority**: Principles I and VIII apply to the SPAs too. Live reload through the gateway is what makes later screen work practical.

**Independent Test**: Start the stack: `/backoffice/` is served by the backoffice container through the gateway. Start development mode, change a visible text in the source, and see it change in the browser at `localhost:8080/backoffice/` without rebuilding the image. A change in the backoffice folder runs only the backoffice workflow.

**Acceptance Scenarios**:

1. **Given** the dev stack with apps, **When** `localhost:8080/backoffice/` is opened, **Then** the built backoffice is served, and the gateway no longer answers 502 for it.
2. **Given** development mode, **When** a source file changes, **Then** the page at `localhost:8080/backoffice/` updates within a few seconds, and sign-in works the same as in the normal stack.
3. **Given** a pull request touching the backoffice folder, **When** CI runs, **Then** the backoffice workflow lints, unit-tests, builds the image and smoke-starts it, and the image-pin and cleanup checks pass.
4. **Given** the backoffice container, **When** it is up, **Then** it reports healthy, runs as a non-root user, and serves the app's files with long caching except for the entry page, which is never cached.

---

### User Story 5 - The first end-to-end test (Priority: P2)

An automated browser test signs in to the backoffice through the gateway as the demo tenant admin, sees the frame with the admin's email, signs out and sees the sign-in screen. It runs on its own throwaway copy of the whole system and leaves nothing behind.

**Why this priority**: It is the first proof that all parts work together in a real browser, and it sets up the end-to-end test project that later features extend.

**Independent Test**: Run the end-to-end command while the dev stack is also running: the test passes, the dev stack is untouched, and afterwards no end-to-end containers or volumes remain, whether the test passed or failed.

**Acceptance Scenarios**:

1. **Given** the end-to-end command, **When** it runs, **Then** it starts its own copy of the stack in the separate project `e2e-test`, runs the browser test, and always tears the copy down.
2. **Given** the dev stack is running at the same time, **When** the end-to-end test runs, **Then** neither touches the other's containers, volumes or ports.
3. **Given** the test fails, **When** the run ends, **Then** the copy is still torn down, the cleanup check passes, and the failure output includes the browser trace.

### Edge Cases

- **Session ends while the app is open** (signed out in another tab). The next API call or reload shows the sign-in screen; no broken half-signed-in frame.
- **User endpoint unreachable** (the gateway is down or answers with an unexpected error). The app shows "The backoffice is unavailable, try again" with a retry, not a blank page.
- **Return path outside the backoffice** in the address bar. The gateway already ignores it; the app only ever sends its own current path.
- **Unknown `signin-error` code.** Shown as the generic "Sign-in failed" message.
- **Customer signs in at the backoffice.** Identity refuses it (014) and the backoffice shows "You don't have access to the backoffice".
- **Browser back button after sign-out.** Cached pages show the sign-in screen once the app asks who is signed in; the entry page is never cached.
- **Very slow user endpoint.** A loading indicator is shown instead of the sign-in screen until the answer arrives.

## Requirements *(mandatory)*

### Functional Requirements

**Sign-in state**

- **FR-001**: On start the backoffice MUST ask the gateway who is signed in, and MUST show a loading state until it knows. It MUST NOT show the frame or any signed-in content before the answer.
- **FR-002**: Without a session the backoffice MUST show a sign-in screen with one action that navigates the browser to the gateway's backoffice login, passing the current backoffice path (with its query, without any `signin-error`) as the return path.
- **FR-003**: With a session the backoffice MUST show its frame: the app name, the user's email (or the user id when there is no email), their user type as "Staff" or "Platform administrator", and a user menu with "Sign out".
- **FR-004**: When opened with `signin-error=<code>`, the backoffice MUST show a plain message for `access_denied`, `invalid_request` and `server_error` (generic message for any other code) on the sign-in screen, and MUST remove the parameter from the address without a reload.

**Sign-out**

- **FR-005**: "Sign out" MUST navigate the browser to the sign-out address the gateway returned with the session. The backoffice MUST NOT build that address itself.

**API calls**

- **FR-006**: Every call to `/api/**` MUST carry the header that names the backoffice app. No other request MUST carry it, and the app MUST NOT add credentials of its own.
- **FR-007**: A 401 answer from any `/api/**` call MUST lead to the sign-in screen, and signing in MUST return the user to the page they were on.
- **FR-008**: A 503 `identity-unavailable` answer MUST show a retry message and keep the user on the page. Retrying MUST repeat the request.
- **FR-009**: The sign-in state, the API header rule and the error handling MUST live in one small module of the backoffice, with no dependency on any OpenID Connect library and no code shared with other apps.

**Serving**

- **FR-010**: The built backoffice MUST be served under `/backoffice/`. Any path under it that is not a file MUST return the app's entry page. The entry page MUST NOT be cached; hashed asset files MUST be cacheable for a long time.
- **FR-011**: The serving container MUST run as a non-root user, report its health, and use only pinned images from the central versions file.
- **FR-012**: The gateway's address for the backoffice MUST match the container's port.

**Development**

- **FR-013**: The normal dev stack MUST include the built backoffice. A separate development mode MUST run the backoffice with live reload inside Docker, served through the gateway at the same address, so sign-in works exactly as in the normal stack. Starting development mode MUST NOT require any tool other than Docker and the existing scripts.

**Delivery**

- **FR-014**: The backoffice MUST live in its own folder with its own build, image, compose file and CI workflow triggered only by its own folder. Its CI MUST lint, unit-test, build the image, smoke-start it, and run the image-pin and cleanup checks.
- **FR-015**: Unit tests MUST cover the sign-in state, the API header rule, the 401 and 503 handling, and the sign-in error messages.

**End-to-end tests**

- **FR-016**: An end-to-end test MUST sign in to the backoffice through the gateway as the demo tenant admin, check the frame shows the admin's email and "Staff", sign out, and check the sign-in screen is shown.
- **FR-017**: End-to-end tests MUST run against their own copy of the stack in the project `e2e-test`, using no named volumes and publishing nothing on the dev ports, and MUST always tear it down (`down -v --remove-orphans`) and run the cleanup check, whether they pass or fail. CI MUST run them.

### Key Entities

- **Session view**: what the backoffice knows about the signed-in user: id, user type, tenant, email and the sign-out address. Comes only from the gateway's user endpoint; nothing is stored in the browser.
- **Sign-in error**: one of the gateway's codes (`access_denied`, `invalid_request`, `server_error`), shown once.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A staff member goes from opening a deep link to seeing the frame on that page in one sign-in, with no more than one extra click (the sign-in button).
- **SC-002**: The end-to-end test signs in, sees the frame and signs out against a fresh copy of the system, and passes in CI.
- **SC-003**: After every end-to-end run, passed or failed, zero end-to-end containers or volumes remain, and the dev stack's containers are unchanged.
- **SC-004**: In development mode, a visible source change appears at `localhost:8080/backoffice/` within 5 seconds without an image rebuild.
- **SC-005**: 100% of `/api/**` calls made by the backoffice carry the app header, and 0% of other requests do (unit tests).
- **SC-006**: A change in the backoffice folder runs only the backoffice workflow (and the end-to-end workflow), and a change in another service's folder does not run the backoffice workflow.

## Assumptions

- Business screens (tenants, staff users, roles) come in 018; this feature has only the frame and a placeholder home page.
- The storefront gets its own copy of the small sign-in module in 017. Sharing is reconsidered if the shared part grows.
- English only; no internationalisation setup yet.
- A plain Angular Material theme; a design pass comes with real screens.
- The gateway's backoffice routes, sign-in endpoints and sign-in error codes are those of 015 and do not change, except the backoffice container port.
- The end-to-end browser runs inside Docker on the same network address as the gateway, so the browser sees `localhost` and the session cookies behave as in development.
- The end-to-end project is new (`tests/e2e`). Its compose setup includes the existing service compose files with overrides for volumes and ports, so the services' own files stay unchanged.
