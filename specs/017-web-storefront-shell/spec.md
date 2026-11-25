# Feature Specification: Storefront Shell

**Feature Branch**: `017-web-storefront-shell`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "017 web-storefront-shell: the customer storefront Angular SPA shell in src/web/storefront, same stack and layout as the 016 backoffice. Served under /shop/ with the shop slug as the first path segment; paths without a valid slug show a 'shop not found' page. Visitors can browse without signing in: the frame shows the shop slug as the header title (no display name; shop name, logo and description come later from a phase-2 shop profile, not from Identity), a home placeholder, and a Sign in button. Sign-in navigates to the gateway's storefront login for the slug; registration happens through Identity's existing 'Create an account' link. Signed in, the header shows an Account menu with the customer's email, an Account page showing email and shop, and Sign out using the session's sign-out address (Identity's confirmation page stays). A session for another shop counts as signed out for this shop. The session module is copied from the backoffice, adapted to the slug and the storefront app header; 401 returns to the signed-out state; 503 identity-unavailable shows the retry banner; sign-in error codes show messages. A second end-to-end test: a new customer registers on the demo shop through the storefront, sees the account page, signs out through the confirmation. English only, plain Material theme."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A visitor opens a shop without signing in (Priority: P1)

A visitor opens `http://localhost:8080/shop/demo/`. The storefront shows the shop's frame right away: the shop's identifier ("demo") as the header title, a welcome placeholder, and a "Sign in" button. Nothing asks them to sign in first.

**Why this priority**: A shop must be browsable by anyone; later features put products here. It is also the base every storefront screen builds on.

**Independent Test**: With no session, open `/shop/demo/` and `/shop/demo/anything`: the frame shows "demo" and "Sign in", with no sign-in wall. Open `/shop/` and `/shop/Not_A_Shop/`: a "shop not found" page.

**Acceptance Scenarios**:

1. **Given** no session, **When** a visitor opens any page under `/shop/<slug>/`, **Then** they see the shop frame with the slug as the title, the page content, and a "Sign in" button.
2. **Given** an address with no shop (`/shop/`) or a malformed shop identifier, **When** it is opened, **Then** a "shop not found" page is shown with no sign-in button.
3. **Given** the storefront is still finding out who is signed in, **When** a page opens, **Then** a loading indicator is shown, and the "Sign in" button does not flicker into view before the answer.
4. **Given** a deep link such as `/shop/demo/some/page`, **When** it is opened or reloaded, **Then** the storefront app loads, not a web-server "not found" page.

---

### User Story 2 - A shopper registers or signs in, and sees their account (Priority: P1)

A shopper presses "Sign in" on the demo shop. Identity's sign-in page for that shop opens. They can sign in, or follow "Create an account" to register. Either way they return to the page they were on, now signed in: the header shows an account menu with their email, and an Account page shows their email and the shop.

**Why this priority**: Customers need an account to order later. Registration already exists in Identity (014); this is the storefront's way in.

**Independent Test**: On `/shop/demo/`, press "Sign in", follow "Create an account", register a new email, and land back on `/shop/demo/` with the email in the account menu. Open `/shop/demo/account`: it shows the email and "demo".

**Acceptance Scenarios**:

1. **Given** the shop frame without a session, **When** the visitor presses "Sign in", **Then** the browser goes to the gateway's sign-in for this shop with the current page as the return path, and after sign-in or registration lands back on that page.
2. **Given** a session for this shop, **When** any storefront page opens, **Then** the header shows an account menu labelled with the customer's email, with "Account" and "Sign out".
3. **Given** a session for this shop, **When** `/shop/<slug>/account` opens, **Then** it shows the customer's email and the shop identifier.
4. **Given** no session, **When** `/shop/<slug>/account` opens, **Then** it says "Sign in to see your account" with a "Sign in" button that returns to the account page.
5. **Given** a session for shop A, **When** the shopper opens shop B, **Then** shop B shows them as not signed in.
6. **Given** sign-in failed or was refused (for example the shop does not exist, or a staff account tried the storefront), **When** the storefront opens with `?signin-error=<code>`, **Then** it shows a plain message for that code above the page and removes the code from the address.

---

### User Story 3 - A shopper signs out (Priority: P1)

From the account menu the shopper chooses "Sign out", confirms on Identity's page, and lands back on the shop as a visitor.

**Why this priority**: Shared devices; and the storefront must use the gateway's sign-out proof.

**Independent Test**: Signed in on `/shop/demo/`, choose "Sign out", confirm, and land on `/shop/demo/` with the "Sign in" button showing.

**Acceptance Scenarios**:

1. **Given** a session, **When** the shopper chooses "Sign out", **Then** the browser goes to the sign-out address the gateway returned with the session, Identity asks for confirmation, and after confirming the browser lands on `/shop/<slug>/` as a visitor.
2. **Given** the shopper signed out, **When** they press "Sign in", **Then** Identity asks for their password again.

---

### User Story 4 - Storefront API calls and an ended session (Priority: P1)

Storefront screens call service APIs through the gateway, always naming the storefront app. When the session has ended, the storefront drops back to the visitor view on the same page. When Identity is briefly unavailable, a retry banner is shown.

**Why this priority**: Later storefront screens (cart, orders) build on it, as the backoffice did in 016.

**Independent Test**: Unit tests with a fake HTTP backend: `/api/**` calls carry the storefront app header; a 401 switches to the visitor view on the same page; a 503 `identity-unavailable` shows the banner and retries on request.

**Acceptance Scenarios**:

1. **Given** any call to `/api/**`, **When** it is sent, **Then** it carries the storefront app header and nothing else is added; other requests carry no app header.
2. **Given** a call answers 401, **When** the answer arrives, **Then** the storefront shows the visitor view of the same page with "Sign in".
3. **Given** a call answers 503 `identity-unavailable`, **When** the answer arrives, **Then** a banner "Sign-in service is unavailable" with "Try again" appears, the session is kept, and "Try again" repeats the request.

---

### User Story 5 - Built, shipped and tested on its own; developed in Docker (Priority: P2)

The storefront has its own folder, build, image, compose file and CI workflow. The normal dev stack serves the built app; development mode serves it with live reload through the gateway. A second end-to-end test registers a customer through the storefront.

**Why this priority**: Principles I, VII and VIII; same as the backoffice.

**Independent Test**: The dev stack serves `/shop/demo/` from the storefront container. Development mode reloads a changed text within 5 seconds. The end-to-end test registers a new customer on the demo shop, sees the account page and signs out, on a throwaway stack that is removed afterwards.

**Acceptance Scenarios**:

1. **Given** the dev stack, **When** `localhost:8080/shop/demo/` is opened, **Then** the built storefront is served and the gateway no longer answers 502 for it.
2. **Given** development mode, **When** a storefront source file changes, **Then** the page updates within 5 seconds, and sign-in works as in the normal stack. Development mode serves both apps with live reload at the same time.
3. **Given** a pull request touching the storefront folder, **When** CI runs, **Then** the storefront workflow lints, unit-tests, builds the image and checks the container's answers (caching, security headers, non-root, health).
4. **Given** the end-to-end command, **When** it runs, **Then** both the backoffice and the storefront tests run against the same throwaway stack, which is always torn down.

### Edge Cases

- **Unknown but well-formed shop** (`/shop/nope/`). The storefront can't tell an unknown shop from a real one without signing in, so it shows the frame. "Sign in" there returns with `signin-error=invalid_request`, shown as "This shop isn't available for sign-in."
- **Staff or platform account on the storefront.** Identity refuses it (014); the message for `access_denied` is "This account can't be used in this shop."
- **Uppercase or odd characters in the shop part** (`/shop/Demo/`). Treated as malformed: "shop not found". Shop identifiers are lowercase.
- **Session for another shop** in the same browser. Shown as a visitor here; signing in here replaces the other shop's session (015).
- **Gateway or user endpoint unreachable.** The frame is shown as a visitor view with a banner "The shop is having trouble right now, try again", not a blank page.
- **Back button after sign-out.** The entry page is never cached, so the visitor view is shown.
- **Backoffice and storefront open in the same browser.** Each sees only its own session.

## Requirements *(mandatory)*

### Functional Requirements

**Shop address**

- **FR-001**: The storefront MUST take the shop identifier from the first path segment under `/shop/`. An absent or malformed identifier (not matching Identity's shop identifier rule) MUST show a "shop not found" page with no sign-in action.
- **FR-002**: The shop frame MUST show the shop identifier as its title. It MUST NOT request a shop display name from any service in this feature.

**Sign-in state**

- **FR-003**: On start the storefront MUST ask the gateway who is signed in for this shop and show a loading indicator until it knows. Visitors (no session, or a session for another shop) MUST see the full shop frame with a "Sign in" action; no page requires sign-in except the account page's content.
- **FR-004**: "Sign in" MUST navigate the browser to the gateway's sign-in for this shop with the current storefront path and query (without any sign-in error) as the return path.
- **FR-005**: With a session for this shop, the header MUST show an account menu labelled with the customer's email (or user id when there is no email), containing "Account" and "Sign out".
- **FR-006**: `/shop/<slug>/account` MUST show the customer's email and the shop identifier when signed in, and a sign-in prompt that returns to the account page when not.
- **FR-007**: When opened with `signin-error=<code>`, the storefront MUST show a storefront-worded message for `access_denied`, `invalid_request` and `server_error` (generic message otherwise) and remove the parameter from the address without a reload.
- **FR-008**: "Sign out" MUST navigate to the sign-out address returned with the session; the storefront MUST NOT build it.

**API calls**

- **FR-009**: Every call to `/api/**` MUST carry the header naming the storefront app; no other request carries it, and no other credentials are added.
- **FR-010**: A 401 from any `/api/**` call MUST switch to the visitor view of the same page.
- **FR-011**: A 503 `identity-unavailable` MUST show a retry banner and keep the session; "Try again" MUST repeat the request.
- **FR-012**: The sign-in state, API header rule and error handling MUST live in one small module of the storefront, copied from the backoffice's and not shared with it, with no OpenID Connect library.

**Serving and delivery**

- **FR-013**: The built storefront MUST be served under `/shop/` with every non-file path returning the entry page; the entry page is never cached; hashed assets are cached for a long time; the same security headers as the backoffice apply; the container runs as non-root and reports health.
- **FR-014**: The gateway's address for the storefront MUST match the container's port.
- **FR-015**: The normal dev stack MUST include the built storefront, and development mode MUST also run the storefront with live reload through the gateway, without tools beyond Docker and the existing scripts.
- **FR-016**: The storefront MUST have its own folder, build, image, compose file and CI workflow triggered only by its own folder, which lints, unit-tests, builds and checks the container's answers.
- **FR-017**: Unit tests MUST cover the shop identifier rule, sign-in state, API header rule, 401 and 503 handling, account page states and sign-in error messages.

**End-to-end**

- **FR-018**: An end-to-end test MUST register a new customer on the demo shop through the storefront (sign in, then "Create an account"), check the account menu and account page, sign out through Identity's confirmation, and check the visitor view, running in the existing `e2e-test` setup together with the backoffice test.

### Key Entities

- **Shop address**: the shop identifier from the URL; lowercase letters, digits and single inner hyphens, 3–63 characters (same rule as Identity and the gateway).
- **Session view**: user id, user type (`customer`), tenant, email, sign-out address and shop identifier, from the gateway's storefront user endpoint for this shop.
- **Sign-in error**: one of the gateway's codes, shown once with storefront wording.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A visitor sees a shop's frame without any sign-in step.
- **SC-002**: A new shopper goes from the shop page to signed in with an account in one registration, landing back on the page they started from.
- **SC-003**: The end-to-end registration test passes on a fresh copy of the system in CI, and no end-to-end containers or volumes remain afterwards.
- **SC-004**: In development mode a storefront source change appears within 5 seconds.
- **SC-005**: 100% of `/api/**` calls from the storefront carry the storefront app header, and 0% of other requests do (unit tests).
- **SC-006**: A session for one shop never shows as signed in on another shop.

## Assumptions

- The shop's display name, logo and description come later from a shop profile owned by the catalogue side (phase 2), not from Identity. Until then the shop identifier is the title (owner decision).
- Identity's sign-out confirmation stays (owner decision), so the end-to-end test confirms it.
- The storefront's session module is a copy of the backoffice's, adapted; sharing is reconsidered if it grows (owner decision from 016).
- No products, cart or orders yet; the home page is a placeholder.
- English only; plain Angular Material theme.
- Gateway routes, storefront sign-in endpoints and error codes are those of 015; only the storefront container port changes.
- The storefront cannot tell an unknown shop from a real one before sign-in; Identity rejects unknown shops at sign-in (014).
