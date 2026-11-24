# Feature Specification: Identity Authentication

**Feature Branch**: `012-identity-authentication`

**Created**: 2026-11-24

**Status**: Draft

**Input**: User description: "012 identity-authentication. Identity becomes the token issuer: a user store with user type and optional tenant; an OpenID Connect server using the code flow with PKCE and a client secret; refresh rotation with a security-stamp check; 15-minute signed (not encrypted) access tokens carrying only `sub`, `tenant_id` and `user_type`; one API scope per service, with the token audience set to the requested scopes; a minimal server-rendered login page; two confidential gateway clients (`backoffice-bff`, `storefront-bff`); a seeded platform admin; a signing key per environment; Identity validating its own tokens locally; and constitution v1.4.0 for the BFF."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A user signs in and the gateway receives tokens (Priority: P1)

The gateway (a confidential client) sends a user to Identity's sign-in. The user enters their email and password on Identity's login page. Identity sends them back with a one-time code, and the gateway exchanges that code, its PKCE verifier and its client secret for an access token and a refresh token.

**Why this priority**: Every protected call in the system needs a token. Nothing past this feature works without a token issuer.

**Independent Test**: In an integration test acting as the gateway client, start the authorization request, submit the login form as the seeded platform admin, follow the redirect, redeem the code, and call `GET /roles` on Identity with the access token. The call is accepted and the identity is recognised: 403 for a platform user, since `/roles` is a tenant route.

**Acceptance Scenarios**:

1. **Given** a registered gateway client and a valid user, **When** the client starts an authorization request with a PKCE challenge and the user signs in, **Then** the user is redirected to the client's registered redirect URI with a one-time code.
2. **Given** a valid one-time code, **When** the client redeems it with the matching PKCE verifier and its client secret, **Then** it receives an access token and a refresh token.
3. **Given** the code was already redeemed, or the verifier doesn't match, or the client secret is wrong, **When** the client tries to redeem it, **Then** the request is rejected and no token is issued.
4. **Given** an authorization request with a redirect URI that isn't registered for the client, **When** it reaches Identity, **Then** Identity refuses it and never redirects to that URI.
5. **Given** an authorization request without a PKCE challenge, **When** it reaches Identity, **Then** it is rejected.

---

### User Story 2 - The token carries only identity, for the right services (Priority: P1)

An access token contains only the subject, the tenant (absent for platform users) and the user type, and no roles or permissions. It is signed and readable, expires after 15 minutes, and is addressed only to the services whose scopes were requested. A service accepts it only if it is in the token's audience.

**Why this priority**: This is the security contract every service relies on, and decision C3 from the architecture.

**Independent Test**: Decode an issued token and check its claims and lifetime. Present a token issued without the `identity` scope to Identity's API and expect 401.

**Acceptance Scenarios**:

1. **Given** a platform admin signs in, **When** the token is decoded, **Then** it holds `sub`, `user_type=platform`, no `tenant_id` and no role or permission claims, and it expires 15 minutes after issue.
2. **Given** a client requested the scopes `identity` and `order`, **When** the token is issued, **Then** its audience is exactly the `identity` and `order` services.
3. **Given** a token whose audience does not include `identity`, **When** it is presented to Identity's API, **Then** the response is 401.
4. **Given** the public signing keys from Identity's discovery document, **When** any service validates a token, **Then** the signature verifies. The token is not encrypted.

---

### User Story 3 - Sessions stay alive and end safely (Priority: P1)

The gateway keeps a user signed in by exchanging the refresh token for a new pair before the access token expires. Every refresh token works once. A refresh is refused when the user's security stamp has changed (password reset, disable) or the user is locked out. Signing out ends the session at Identity.

**Why this priority**: The 15-minute token lifetime only works with safe refresh, and the stamp check is the decided way to cut off a compromised or disabled account.

**Independent Test**: Refresh twice with the same refresh token: the first succeeds, the second fails. Change the user's security stamp and refresh with a fresh token: it fails. Sign out, then refresh: it fails.

**Acceptance Scenarios**:

1. **Given** a valid refresh token, **When** the client redeems it, **Then** it receives a new access token and a new refresh token, and the old refresh token stops working.
2. **Given** a refresh token that was already used, **When** it is redeemed again, **Then** the request is rejected and that token chain is revoked, which signals a stolen token.
3. **Given** the user's security stamp changed after the token was issued, **When** the client refreshes, **Then** the request is rejected.
4. **Given** a signed-in user, **When** the gateway sends them to Identity's sign-out with a registered post-logout URI and they confirm, **Then** their Identity session ends, they are redirected to that URI, and their refresh tokens stop working.

---

### User Story 4 - The login page is safe and clear (Priority: P2)

The login page asks for an email and a password. Wrong credentials show one generic message that doesn't reveal whether the email exists. After 5 failed attempts the account is locked for 10 minutes, and the page says the account is temporarily locked. The form is protected against cross-site request forgery.

**Why this priority**: It's the only interactive surface in this feature, and it's where credential attacks land.

**Independent Test**: Submit a wrong password for an existing email and for an unknown email, and compare the responses (same message, same status). Fail 5 times, then submit the right password: still refused, with the lockout message. Advance time past 10 minutes: sign-in works.

**Acceptance Scenarios**:

1. **Given** an unknown email or a wrong password, **When** the form is submitted, **Then** the page shows the same generic "email or password is incorrect" message in both cases.
2. **Given** 5 consecutive failed attempts for one account, **When** the correct password is submitted next, **Then** sign-in is refused with a "temporarily locked, try again later" message.
3. **Given** a locked account, **When** 10 minutes have passed, **Then** the correct password signs the user in, and the failure count is reset.
4. **Given** a form post without a valid anti-forgery token, **When** it is submitted, **Then** it is rejected.
5. **Given** a phone-sized screen, **When** the page is opened, **Then** it's usable without horizontal scrolling and has a simple Material-like look.

---

### User Story 5 - A platform admin exists from the first start (Priority: P2)

On first start with an empty database, Identity creates a platform administrator from configuration. In development the values come from the dev settings file. In production they must be supplied through the environment. There is no built-in default, and Identity refuses to start when no platform admin exists and none is configured.

**Why this priority**: Without a first user there is no way in, and tenant management in 013 is done by the platform admin.

**Independent Test**: Start against an empty database with the admin settings and confirm the admin can sign in. Start against an empty database without them and confirm startup fails with a clear message. Restart with the settings changed and confirm the existing admin is not overwritten.

**Acceptance Scenarios**:

1. **Given** an empty user store and configured admin email and password, **When** Identity starts, **Then** a platform user with no tenant exists, and they can sign in.
2. **Given** an empty user store and no admin settings, **When** Identity starts, **Then** startup fails with a message naming the missing settings.
3. **Given** a platform admin already exists, **When** Identity starts with different admin settings, **Then** the existing admin is unchanged.
4. **Given** the configured admin password breaks the password policy, **When** Identity starts against an empty store, **Then** startup fails with the policy reason.

---

### User Story 6 - Tokens stay valid across restarts (Priority: P2)

In development, Identity creates its signing key on first start and keeps it in persistent storage, so tokens and services' cached keys survive a restart. In production the key comes from a mounted certificate. Tests use a throwaway key.

**Why this priority**: A new key on every restart would sign everyone out and invalidate every service's cached keys.

**Independent Test**: In the dev stack, obtain a token, restart Identity, and confirm the same token is still accepted and the published key set is unchanged.

**Acceptance Scenarios**:

1. **Given** the dev stack, **When** Identity starts for the first time, **Then** a signing key is created and stored. **When** it restarts, **Then** the same key is used.
2. **Given** production configuration with a mounted certificate, **When** Identity starts, **Then** it signs with that certificate. **Given** no certificate is configured in production, **Then** startup fails.
3. **Given** a test run, **When** Identity starts, **Then** it uses an in-memory key and writes nothing to disk.

---

### User Story 7 - Identity's own API trusts its own tokens (Priority: P2)

Identity's API validates tokens it issued itself, locally, with no network call to itself. It keeps the same rules as every other service: tenant routes by default, platform-only and allow-listed routes, 401 for invalid or malformed identities, 403 for the wrong caller or a missing permission. The no-roles route test still passes, and the new OpenID Connect and login endpoints are reviewed allow-list entries.

**Why this priority**: It replaces the placeholder authority from 011 and keeps the security baseline intact.

**Independent Test**: Run Identity's whole test suite, including the route test. Call `GET /roles` with a token Identity issued in the same test.

**Acceptance Scenarios**:

1. **Given** a token issued by Identity with the `identity` scope, **When** it calls `GET /roles` as a staff user holding `identity.roles.view`, **Then** the response is 200.
2. **Given** the no-roles route test, **When** it runs, **Then** it passes, and it reports the protocol and login endpoints as allow-listed.
3. **Given** Identity's token endpoint is unreachable from inside the container, **When** its API validates a token, **Then** validation still works, because it uses the local key.

---

### User Story 8 - The constitution records the BFF (Priority: P3)

The constitution states that browser apps use the BFF: the gateway is a confidential client doing the code flow with PKCE, and browsers hold only an encrypted, HttpOnly, Secure, SameSite=Strict cookie plus an anti-forgery header. Tokens never reach the browser, and everything is served from one origin through the gateway.

**Why this priority**: It's a locked decision that needs to be on record before the gateway is built in 014.

**Independent Test**: Read the SPA-auth line in the constitution and its footer.

**Acceptance Scenarios**:

1. **Given** the amended constitution, **When** a reviewer reads the SPA authentication decision, **Then** it describes the BFF as above and no longer mentions code + PKCE inside the SPA.
2. **Given** the footer, **Then** the version is 1.4.0 and the last-amended date is updated.

---

### Edge Cases

- **A disabled user** (security stamp changed, or account disabled) still holds a valid access token for up to 15 minutes. This is the accepted window (decision C5). Refresh is refused.
- **A client asks for a scope that isn't registered**, or one it isn't allowed. The request is rejected.
- **A client asks for no API scope.** Access tokens are issued only with at least one API scope. Otherwise the request is rejected.
- **Two refreshes race with the same refresh token.** At most one succeeds.
- **The login page is called directly, without an authorization request.** Signing in works, and the user lands on a minimal "signed in" page, not on an open redirect.
- **The `returnUrl` on the login page points outside Identity.** It is ignored, because only local return URLs are allowed.
- **The signing certificate in production expires soon.** Out of scope for automation here. Rotation is manual, by mounting a new certificate (both keys can be published during rotation later).
- **The clock moves during a lockout.** Lockout uses the application's time provider, so tests can control it.

## Requirements *(mandatory)*

### Functional Requirements

**Users**

- **FR-001**: Identity MUST store users with a unique email, a password hash, a user type (`staff`, `customer`, `platform`), a tenant (required for staff and customers, absent for platform), a security stamp, and lockout state.
- **FR-002**: Passwords MUST be at least 8 characters, with the platform's default complexity rules otherwise.
- **FR-003**: After 5 consecutive failed sign-in attempts the account MUST be locked for 10 minutes. A successful sign-in resets the count.

**OpenID Connect server**

- **FR-004**: Identity MUST publish a discovery document and a public key set, and offer authorization, token and end-session endpoints.
- **FR-005**: Only the authorization code flow with PKCE (S256) and a client secret is allowed. Requests without PKCE, other grant types (implicit, password, client credentials for these clients) and public clients MUST be rejected.
- **FR-006**: Access tokens MUST be signed, not encrypted, valid for 15 minutes, and contain only `sub`, `user_type` and, for non-platform users, `tenant_id`, plus the standard protocol claims (issuer, audience, issued-at, expiry, token id, scope, client). No role or permission claims.
- **FR-007**: The API scopes `identity`, `product`, `inventory`, `order` and `payment` MUST be registered, each mapping to its service as an audience. A token's audience MUST be exactly the API scopes granted. Requests MUST include at least one API scope.
- **FR-008**: Refresh tokens MUST be issued when the `offline_access` scope is requested, rotate on every use, and be refused when reused, when the user's security stamp changed since issue, or when the user is locked out. In 012, disabling a user means changing their security stamp. Feature 013 adds an explicit disabled state and checks it at refresh too.
- **FR-009**: Signing out at the end-session endpoint MUST end the Identity session after confirmation, revoke the user's tokens for that client, and redirect only to a registered post-logout URI.
- **FR-010**: Authorization requests with a redirect URI that isn't registered for the client MUST be rejected without redirecting.

**Clients and scopes**

- **FR-011**: Identity MUST register two confidential clients on startup, `backoffice-bff` and `storefront-bff`, each with its secret, its redirect URI and its post-logout URI from configuration, allowed the code flow, refresh tokens and all API scopes. Registration MUST be idempotent and update changed settings.
- **FR-012**: Client secrets MUST come from configuration, with no defaults in code. Dev values are in the dev settings file.

**Login page**

- **FR-013**: Identity MUST serve a server-rendered login page with email and password fields, protected against cross-site request forgery, usable on a phone screen, with a simple Material-like look and no single-page-app framework.
- **FR-014**: Failed sign-ins MUST show one generic message, identical for an unknown email and a wrong password. A locked account MUST show a lockout message without revealing the remaining failure count.
- **FR-015**: The login page MUST accept only local return URLs.
- **FR-016**: Identity MUST serve a sign-out confirmation page.

**Platform admin**

- **FR-017**: On startup with no platform user, Identity MUST create one from the configured email and password. It MUST fail startup with a clear message when either is missing or the password breaks the policy. An existing platform admin MUST never be modified by the seed.

**Signing keys**

- **FR-018**: In development, Identity MUST create a self-signed signing certificate on first start and keep it in persistent storage, reusing it on every restart.
- **FR-019**: In production, Identity MUST sign with a mounted certificate, and MUST fail startup if none is configured.
- **FR-020**: In tests, Identity MUST use an in-memory signing key.

**Identity's own API**

- **FR-021**: Identity's API MUST validate tokens locally against its own keys, accepting only tokens whose audience includes `identity`. It keeps the route categories, the fallback policy and the 401/403 behaviour of the shared setup.
- **FR-022**: The OpenID Connect endpoints, the login and sign-out pages and their assets MUST be marked allow-listed, and the no-roles route test MUST pass.

**Constitution**

- **FR-023**: The constitution MUST be amended to v1.4.0 (MINOR), replacing the SPA authentication decision with the BFF as described in User Story 8, and updating the last-amended date.

### Key Entities

- **User**: email (unique, used for sign-in), password hash, user type, tenant (optional), security stamp, failed-attempt count, lockout end.
- **Client application**: client id, secret (hashed), redirect URIs, post-logout redirect URIs, allowed flows and scopes.
- **API scope**: a name (`identity`, …) and the service audience it maps to.
- **Authorization and token records**: the protocol's own records for codes, refresh tokens and revocation.
- **Signing key**: the certificate used to sign tokens, published as a public key.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The full sign-in flow (authorize, login, code redemption) completes in an automated test in under 5 seconds.
- **SC-002**: 100% of issued access tokens contain only the allowed identity claims, with no role or permission claims, and expire 15 minutes after issue.
- **SC-003**: A reused refresh token is refused 100% of the time, and so is any refresh after a security-stamp change.
- **SC-004**: The 6th sign-in attempt after 5 failures is refused even with the right password. Sign-in works again after 10 minutes.
- **SC-005**: Responses for an unknown email and a wrong password are indistinguishable (same status and message).
- **SC-006**: Restarting Identity in the dev stack invalidates 0 previously issued tokens.
- **SC-007**: The no-roles route test and the full Identity suite pass on every pull request.

## Assumptions

- The gateway doesn't exist yet. Tests act as the confidential client, and the dev redirect URIs point at the future gateway origin (`http://localhost:8080/...`), set in the dev settings file.
- Only the platform admin can sign in during 012. Staff and customers arrive with tenants in 013, which will also add customer sign-in with the tenant slug (decision C6).
- The audience value for each service equals its scope name (`identity`, `product`, …), matching the `Authentication:Audience` setting already used by 011.
- Identity's own API keeps using the BuildingBlocks route categories and the identity middleware. Only token validation switches from the shared authority setup to local validation.
- Account self-registration, password reset and email confirmation are out of scope. Platform-admin password change can come later.
- The signing certificate's dev volume belongs to the Identity API service and is removed by `dev.ps1 reset`.
- The 15-minute window after a disable is accepted (decision C5). Immediate revocation is not in scope.
- Refresh tokens last 12 hours, rolling: each use issues a new one, so an active user stays signed in through a working day.
- Identity requires HTTPS by default. The dev stack and the tests turn this off with a setting, and in production the gateway handles TLS (014).
