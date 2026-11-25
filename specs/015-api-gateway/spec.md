# Feature Specification: API Gateway

**Feature Branch**: `015-api-gateway`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "015 api-gateway: YARP gateway with BFF. src/gateway/ with own Dockerfile, compose, CI; the only container publishing a port. Routes: /backoffice/** and /shop/** to SPA containers (stub upstreams in tests; real nginx containers come with SPA features), /connect/** and /account/** to Identity, /api/<service>/** with PathRemovePrefix and the spa policy. BFF: gateway is confidential OIDC client (code + PKCE); one OIDC scheme and one cookie per SPA (backoffice and storefront), endpoints /bff/backoffice/{login,logout,user} and /bff/shop/{slug}/{login,logout,user}; one storefront cookie, logging in to another shop replaces the session. Cookie encrypted, HttpOnly, Secure, SameSite=Strict, plus anti-forgery header on unsafe requests. Tokens stay in the encrypted cookie; server-side ticket store deferred to phase 5 with Redis. Gateway Data Protection keys in a Docker volume. Gateway swaps cookie for bearer token when proxying /api/**, 401 when no session. Storefront login adds tenant=<slug> authorization parameter. Refresh near expiry, serialized per session with an in-memory lock (single instance; distributed lock phase 5). Carry-overs from 012 review: UseForwardedHeaders in Identity behind the gateway, one refresh at a time per session, send id_token_hint on sign-out. Rate limiting deferred to phase 5."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A staff member signs in to the backoffice through the gateway (Priority: P1)

A staff member opens the backoffice at the gateway's address and chooses to sign in. The gateway sends them to Identity's sign-in page, and after they sign in they come back to the backoffice with a session. The browser holds only the gateway's session cookie and never sees a token. The backoffice asks the gateway who is signed in and shows the user.

**Why this priority**: Every backoffice screen needs a signed-in staff member, and this is the only way to get one once tokens stay out of the browser.

**Independent Test**: Against Identity (real or a stand-in issuing the same kind of tokens), call the backoffice login endpoint, follow the redirects through sign-in, and return to the gateway. The browser receives only the gateway's session cookie. The user endpoint then reports the staff member's id, user type and tenant, and contains no token.

**Acceptance Scenarios**:

1. **Given** no session, **When** the browser opens the backoffice login endpoint, **Then** it is redirected to Identity's authorization endpoint as the backoffice client, using the authorization code flow with PKCE (S256).
2. **Given** a staff member signs in at Identity, **When** Identity redirects back to the gateway, **Then** the gateway redeems the code with its client secret, sets the backoffice session cookie, and redirects to a local backoffice page (the requested return path, or the backoffice start page).
3. **Given** a backoffice session, **When** the backoffice calls the user endpoint, **Then** it gets the user's id, user type, tenant and email, and no access, refresh or identity token.
4. **Given** no session, **When** the backoffice calls the user endpoint, **Then** it gets 401 and no redirect.
5. **Given** a login request with a return path outside the gateway's origin, **When** sign-in completes, **Then** the gateway ignores it and lands on the backoffice start page.

---

### User Story 2 - A shopper signs in to a tenant's storefront through the gateway (Priority: P1)

A shopper on acme's storefront (`/shop/acme/...`) chooses to sign in. The gateway sends them to Identity for the acme shop, where they sign in or register, and they come back to acme's storefront with a session for acme.

**Why this priority**: Customers can't place orders without a session, and Identity requires the tenant to arrive with the request (014).

**Independent Test**: Call the storefront login endpoint for `acme` and check that the authorization request to Identity is for the storefront client and carries `tenant=acme`. Complete sign-in: the user endpoint for `acme` reports acme's customer. Then log in to `globex`: the acme session is replaced by the globex one.

**Acceptance Scenarios**:

1. **Given** no session, **When** the browser opens the storefront login endpoint for slug `acme`, **Then** it is redirected to Identity as the storefront client with the authorization parameter `tenant=acme`.
2. **Given** the shopper signs in or registers at Identity, **When** they come back, **Then** the gateway sets the storefront session cookie and redirects to a local page under `/shop/acme/`.
3. **Given** a storefront session for acme, **When** the storefront calls the user endpoint for `acme`, **Then** it gets the customer's id, user type, tenant and email; for `globex` it gets 401.
4. **Given** a storefront session for acme, **When** the shopper signs in to globex's storefront, **Then** the globex session replaces the acme session.
5. **Given** a slug that is not a well-formed slug, **When** the login endpoint is opened, **Then** the gateway answers 404 and does not contact Identity. An unknown but well-formed slug is passed through, and Identity rejects it (014).
6. **Given** a staff member is signed in to the backoffice and a shopper session exists in the same browser, **When** either app calls its endpoints, **Then** each sees only its own session.

---

### User Story 3 - The SPAs call service APIs with the session, never with a token (Priority: P1)

A signed-in SPA calls `/api/<service>/...` on the gateway. The gateway attaches the session's access token as a bearer token, removes the `/api/<service>` prefix, and forwards the call to the service. Without a session, or without the anti-forgery header, the call never reaches the service.

**Why this priority**: This is how every later feature's screens talk to services. It is also the gateway's main security boundary.

**Independent Test**: With a stub upstream that records what it receives: a signed-in call to `/api/identity/users` reaches the stub as `/users` with `Authorization: Bearer <session access token>`, without the browser's cookies and without any `Authorization` header the browser sent. Without a session the answer is 401; without the anti-forgery header it is rejected; neither reaches the stub.

**Acceptance Scenarios**:

1. **Given** a session and the anti-forgery header, **When** the SPA calls `/api/<service>/<path>`, **Then** the service receives `/<path>` with the query string unchanged and the session's access token as the bearer token.
2. **Given** no session, **When** the SPA calls any `/api/**` route, **Then** the gateway answers 401 problem details, with no redirect, and does not forward the call.
3. **Given** a session but no anti-forgery header, **When** the SPA calls any `/api/**` route, **Then** the gateway rejects it and does not forward it.
4. **Given** a browser request carrying its own `Authorization` header or cookies, **When** it is forwarded, **Then** the service receives neither; only the gateway's bearer token.
5. **Given** a service answers 401, 403, 404, 409, 422 or 400, **When** the gateway forwards the answer, **Then** the SPA receives the status and problem details unchanged.
6. **Given** a path under `/api/` for a service the gateway does not know, **When** it is called, **Then** the answer is 404.
7. **Given** both a backoffice and a storefront session in the same browser, **When** an API call arrives, **Then** the anti-forgery header names which app is calling, and the gateway uses only that app's session.

---

### User Story 4 - Sessions stay alive without the user noticing, and end cleanly (Priority: P1)

Access tokens live 15 minutes. While the user keeps working, the gateway refreshes the token before it expires and stores the new tokens in the session cookie. When Identity refuses a refresh (the user was disabled, the session was revoked), the session ends. Signing out ends the session at the gateway and at Identity.

**Why this priority**: Without refresh users would be thrown out every 15 minutes; without a clean refresh, parallel API calls would revoke the whole session (012: reuse revokes the chain).

**Independent Test**: Sign in, move the clock past the refresh threshold, and send ten API calls in parallel: Identity sees exactly one refresh, all ten calls succeed, and the response updates the cookie. Disable the user in Identity, then pass the threshold again: the next call gets 401 and the cookie is cleared. Sign out: Identity's end-session endpoint receives the session's identity token as a hint, and the gateway session is gone.

**Acceptance Scenarios**:

1. **Given** a session whose access token expires within the refresh threshold, **When** an API call arrives, **Then** the gateway refreshes before forwarding, forwards with the new access token, and returns the updated session cookie.
2. **Given** several calls of the same session arrive while a refresh is needed, **When** they are processed, **Then** exactly one refresh is sent to Identity and every call uses its result.
3. **Given** a call that still carries the old session cookie after a refresh already happened, **When** it arrives shortly after, **Then** the gateway uses the refreshed tokens and does not send the old refresh token again.
4. **Given** Identity refuses the refresh, **When** the call is processed, **Then** the gateway answers 401, clears that app's session cookie, and does not forward the call.
5. **Given** a session, **When** the SPA asks to sign out with the session's sign-out proof (from the user endpoint), **Then** the gateway clears its session cookie and redirects the browser to Identity's end-session endpoint with the identity token as a hint and the app's registered post-logout address, and Identity revokes only that session's tokens.
6. **Given** a sign-out request without the sign-out proof, **When** it arrives, **Then** it is rejected and the session stays.

---

### User Story 5 - One address for the whole system (Priority: P1)

Users, developers and tests reach everything through the gateway's single address: the two SPAs, Identity's sign-in pages and protocol endpoints, and the service APIs. Identity, behind the gateway, builds its URLs and issuer from the address the browser used.

**Why this priority**: The BFF cookie, `SameSite=Strict` and the "no CORS" design all depend on one origin. Identity's issuer is already the gateway's address.

**Independent Test**: Start the dev stack. Only the gateway publishes an application port. Identity's discovery document fetched through the gateway lists the gateway's address for every endpoint. `/backoffice/` and `/shop/acme/` reach their SPA upstreams (stubs in tests).

**Acceptance Scenarios**:

1. **Given** the dev stack is up, **When** the published ports are listed, **Then** the gateway is the only application container with a published port. Databases and the broker keep their local-only ports for developer tools.
2. **Given** a request to `/connect/**` or `/account/**` (and the static assets Identity's pages need), **When** it reaches the gateway, **Then** it is forwarded to Identity with the original host, scheme and client address in forwarded headers.
3. **Given** Identity behind the gateway, **When** it builds redirects, sign-in links and the discovery document, **Then** they use the gateway's public address and scheme. Identity trusts forwarded headers only from the private container networks, and only for the gateway's host. Its port is not published, so only containers on the compose network can reach it.
4. **Given** a request to `/backoffice/**` or `/shop/**`, **When** it reaches the gateway, **Then** it is forwarded to the matching SPA upstream with its path unchanged, without authentication. A missing or unreachable upstream gives 502 (504 on timeout).
5. **Given** a request to a path the gateway has no route for, **When** it arrives, **Then** the answer is 404.

---

### User Story 6 - The gateway is built, tested and shipped on its own (Priority: P2)

The gateway lives in its own folder with its own build, Docker image, compose file and CI workflow, like a service.

**Why this priority**: Principle I applies to the gateway too. It is needed to merge, but brings no user value on its own.

**Independent Test**: Change a file in the gateway folder: only the gateway workflow runs, builds the image, runs the tests and starts the image. Changing Identity does not run the gateway workflow.

**Acceptance Scenarios**:

1. **Given** the repository, **When** the gateway is built, **Then** nothing outside its folder is built and only versioned packages are referenced.
2. **Given** a pull request touching the gateway folder, **When** CI runs, **Then** the gateway workflow builds, tests and smoke-starts the image, and the image-pin and test-cleanup checks pass.
3. **Given** the gateway container, **When** it is up, **Then** it reports liveness and readiness. It is ready once its configuration is valid; it does not wait for Identity or the SPAs.

### Edge Cases

- **Session cookie too large for one cookie.** The tokens are split across several cookies and reassembled; sign-in, refresh and sign-out work the same.
- **Gateway restart.** Sessions survive, because the keys that encrypt the cookie live in a volume. With a new volume, old cookies are treated as no session (401), not as an error.
- **Tampered or foreign cookie.** Treated as no session.
- **Sign-in abandoned or refused at Identity** (wrong user type, unknown tenant, user cancels). The gateway shows or redirects to an error page of the SPA and creates no session; it never loops.
- **Callback replayed or forged** (old code, wrong state). Rejected; no session is created.
- **Refresh while Identity is down.** The call fails with 503 and the session stays, so the next call can try again. Only a refusal from Identity ends the session.
- **Access token already expired** (laptop asleep longer than the token's life, but the refresh token is still valid). Refreshed before forwarding, like a near-expiry token.
- **Refresh token expired.** Treated like a refusal: 401 and the session cookie is cleared.
- **Request to `/bff/**` from another site.** `SameSite=Strict` keeps the session cookie off it. Sign-out also needs the session's sign-out proof, which a cross-site page cannot read. Login and user are safe GETs.
- **Storefront path for a slug the session isn't for.** The user endpoint says 401 for that slug. API calls still use the one storefront session; the services scope them to the session's tenant.
- **Request id and tracing.** Each forwarded call carries a correlation id, and the gateway's trace joins the service's trace.

## Requirements *(mandatory)*

### Functional Requirements

**Routing**

- **FR-001**: The gateway MUST be the single public entry point and route by path: `/backoffice/**` and `/shop/**` to the matching SPA upstream with the path unchanged; `/connect/**`, `/account/**` and the static assets Identity's pages use to Identity with the path unchanged; `/api/<service>/**` to that service with the `/api/<service>` prefix removed; `/bff/**` handled by the gateway itself. Anything else MUST answer 404.
- **FR-002**: Upstream addresses MUST come from configuration. The gateway MUST start and be ready even if an upstream is down, and answer 502 for routes whose upstream cannot be reached (504 on timeout).
- **FR-003**: Identity is the only service with API routes in this feature (`/api/identity/**`). Adding a service MUST need only configuration.
- **FR-004**: Forwarded calls MUST carry the original host, scheme and client address as forwarded headers, and trace context, whose trace id is the correlation id.

**Sessions (backend for frontend)**

- **FR-005**: The gateway MUST act as the confidential client `backoffice-bff` for the backoffice and `storefront-bff` for the storefront, using the authorization code flow with PKCE (S256) and its client secret. Client secrets MUST come from configuration and never reach the browser.
- **FR-006**: Each SPA MUST have its own session cookie. Session cookies MUST be encrypted, `HttpOnly`, `Secure` and `SameSite=Strict`, and MUST hold the access, refresh and identity tokens. No token may appear in any response body or browser-readable header.
- **FR-007**: The gateway MUST offer, for the backoffice, `/bff/backoffice/login`, `/bff/backoffice/logout` and `/bff/backoffice/user`, and for the storefront `/bff/shop/{slug}/login`, `/bff/shop/{slug}/logout` and `/bff/shop/{slug}/user`. The sign-in callbacks MUST be the redirect addresses already registered in Identity for each client.
- **FR-008**: Login MUST accept an optional return path and follow it only if it is a local path inside that SPA's area (`/backoffice/` or `/shop/{slug}/`); otherwise the SPA's start page.
- **FR-009**: Storefront login MUST send the slug to Identity as the authorization parameter `tenant`. A malformed slug MUST answer 404 without contacting Identity. A new storefront sign-in MUST replace any existing storefront session.
- **FR-010**: The user endpoint MUST answer 401 without a session (and, for the storefront, for a slug other than the session's tenant), and otherwise return the user's id, user type, tenant id, email and the sign-out address with its proof. It MUST NOT redirect.
- **FR-011**: The keys that encrypt session cookies MUST persist across gateway restarts in the dev stack, in a volume owned by the gateway. Tests MUST use throwaway keys.

**Anti-forgery**

- **FR-012**: Every `/api/**` call MUST carry an anti-forgery header that a cross-site page cannot send without the browser's permission. The header value MUST name the calling app (backoffice or storefront), and the gateway MUST use only that app's session. Calls without a valid header MUST be rejected without being forwarded. Sign-out is a browser navigation, which cannot carry a header, so it MUST carry the session's own random sign-out proof instead, which only the user endpoint reveals; a missing or wrong proof MUST be rejected and the session kept.

**Forwarding with tokens**

- **FR-013**: For `/api/**`, the gateway MUST require a session of the named app (401 problem details otherwise, never a redirect), attach its access token as the bearer token, and remove the browser's cookies and any browser-sent `Authorization` header before forwarding.
- **FR-014**: The gateway MUST pass service responses through unchanged, including status codes and problem details.

**Refresh**

- **FR-015**: Before forwarding, the gateway MUST refresh the session's tokens when the access token is expired or expires within a configurable threshold (default 1 minute), forward with the new access token, and return the updated session cookie with the response.
- **FR-016**: Refresh MUST be serialized per session: concurrent calls of one session MUST cause exactly one refresh, and calls still carrying the pre-refresh cookie shortly after MUST use the refreshed tokens instead of sending the used refresh token again. This holds for a single gateway instance; across instances it is deferred to phase 5.
- **FR-017**: When Identity refuses a refresh, the gateway MUST answer 401, clear that app's session cookie, and not forward the call. When Identity cannot be reached, the gateway MUST answer 503 and keep the session.

**Sign-out**

- **FR-018**: Sign-out MUST clear the app's session cookie and redirect to Identity's end-session endpoint with the session's identity token as `id_token_hint` and the client's registered post-logout address, so Identity revokes only that session. Sign-out without a session MUST just land on the SPA's start page.

**Identity behind the gateway**

- **FR-019**: Identity MUST honor forwarded host, scheme and client address only from configured networks and only for configured hosts, so its issuer, discovery document, redirects and sign-in links use the gateway's public address. With no networks configured it MUST trust none.
- **FR-020**: Identity's application port MUST no longer be published in the dev stack. Developer tools and scripts reach Identity through the gateway.
- **FR-021**: Identity MUST put the user type, tenant and email into the identity token issued to the gateway, so the user endpoint can answer without reading access tokens. Access tokens keep only `sub`, `tenant_id` and `user_type`.

**Delivery**

- **FR-022**: The gateway MUST live in its own folder with its own solution, Docker image, compose file, health endpoints and CI workflow triggered only by its own folder, and MUST use only versioned shared packages.
- **FR-023**: Every gateway image and test fixture MUST use pinned images from the central versions file.

### Key Entities

- **App session**: one per SPA per browser. Holds the user's id, user type, tenant, email, the access, refresh and identity tokens and the access token's expiry, sealed in the encrypted session cookie. The storefront session also holds the slug it was signed in for.
- **Route**: a path pattern, an upstream, whether a session is required, and how the path is changed.
- **Client registration** (in Identity, from 012): client id, secret, redirect address and post-logout address for each SPA.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A staff member and a shopper each complete sign-in through the gateway and back in one automated flow, and no response body or browser-readable header in either flow contains a token.
- **SC-002**: 100% of `/api/**` calls without a session or without the anti-forgery header are rejected and never reach a service.
- **SC-003**: With 10 parallel calls of one session past the refresh threshold, exactly one refresh reaches Identity and all 10 calls succeed.
- **SC-004**: A user disabled in Identity loses API access through the gateway within one access-token lifetime (15 minutes) plus the refresh threshold.
- **SC-005**: After sign-out, the old session cookie gives 401 at the gateway and the session's refresh token is refused by Identity.
- **SC-006**: In the dev stack, exactly one application port is published, and Identity's discovery document lists only the gateway's address.
- **SC-007**: A change in the gateway folder runs only the gateway workflow, and a change elsewhere does not run it.

## Assumptions

- Real SPA containers (nginx) come with the SPA features. Until then the dev stack has no SPA upstreams, so `/backoffice/**` and `/shop/**` answer 502 there, and tests use stub upstreams.
- The gateway runs as one instance until phase 5. Then a server-side session store and a shared refresh lock come with Redis, along with per-IP rate limiting.
- Sessions keep tokens inside the encrypted cookie. A server-side session store, and with it immediate server-side session kill, are phase 5.
- The registered sign-in callback addresses stay as they are in Identity (`/bff/backoffice/signin-oidc`, `/bff/storefront/signin-oidc`). The registered post-logout addresses move from the SPA start pages to the gateway's sign-out callbacks (`/bff/backoffice/signout-callback-oidc`, `/bff/storefront/signout-callback-oidc`), so the gateway can land a shopper back on their own shop (`/shop/acme/`) rather than on `/shop/`.
- In the dev stack the gateway runs on plain HTTP at `localhost`, which browsers treat as a secure context for `Secure` cookies. TLS termination at the gateway is part of the deployment phase.
- The gateway checks only that a session exists. Authorization stays in the services (principle VI): they validate the token again and answer 403.
- The gateway's 401 comes from a missing or ended session. It does not check the signature of the access tokens again, since it got them from Identity itself over the back channel; the services validate every token. The constitution's request-flow row ("Gateway validates the JWT") predates the BFF decision, and the plan's Constitution Check records this.
- Identity's sign-in pages keep their own `SameSite=Lax` login cookie (012), separate from the gateway's session cookies.
