---

description: "Task list for feature 015 API Gateway"
---

# Tasks: API Gateway

**Input**: Design documents from `specs/015-api-gateway/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**:
- No comments in any file (C#, cshtml, YAML, Dockerfile, scripts).
- No tokens in any response body or browser-readable header.
- Gateway tests use no containers.
- The build stays green after every task.
- Paths are relative to `src/gateway/` unless they start with `src/`, `deploy/`, `scripts/`, `tools/` or `.github/`.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Create `Gateway.slnx`, `Directory.Build.props` (same settings as `src/services/identity/Directory.Build.props`, no MinVer tag prefix), `Directory.Packages.props` (research.md R1 versions, plus `Microsoft.Extensions.TimeProvider.Testing` at the current 10.x release), `nuget.config` (nuget.org only), `Gateway.Api/Gateway.Api.csproj` (Web SDK) and `Gateway.Tests/Gateway.Tests.csproj` (xUnit v3, Microsoft Testing Platform, as Identity.Tests); add both projects to `InventoryAndOrder.slnx`
- [X] T002 [P] `Dockerfile` modelled on `src/services/identity/Dockerfile`: build arguments `DOTNET_SDK_IMAGE` and `DOTNET_ASPNET_IMAGE` without defaults, no `packages` context, a `/keys` folder owned by `$APP_UID`, port 8080
- [X] T003 [P] `compose.yaml`: service `gateway`, profile `apps`, `env_file`-supplied settings per data-model.md Configuration, port `127.0.0.1:${GATEWAY_PORT}:8080`, volume `gateway-keys:/keys`, a `wget` health check on `/health/ready`, `restart: on-failure`, no `depends_on`. Include it in `deploy/compose.yaml` and add `GATEWAY_PORT=8080` to `deploy/dev.env`

---

## Phase 2: Foundational

- [X] T004 `Gateway.Api/Configuration/GatewaySettings.cs` with validation per data-model.md Configuration (`PublicOrigin`, `Identity:InternalAddress`, `Clients:*:Secret`, `Refresh:Threshold`, `Keys:Path`), read and validated at startup; an invalid value throws. Tests first in `Gateway.Tests/SettingsTests.cs`: each invalid value stops the host
- [X] T005 [P] `Gateway.Api/Problems.cs`: `AddProblemDetails` with the `traceId` extension, and a helper that writes the problem types in contracts/bff.md
- [X] T006 [P] `Gateway.Api/Sessions/BffApp.cs`: the two apps with their cookie scheme, OIDC scheme, cookie name (data-model.md App session; `__Host-Http-` prefix only when `PublicOrigin` is `https`, research.md R2), client id, start page, callback and signed-out callback paths (research.md R2, R9). Unit test `AppsTests` in `Gateway.Tests/UnitTests.cs` for both prefix cases
- [X] T007 `Gateway.Api/Program.cs` and `appsettings.json`: YARP from the `ReverseProxy` section with the routes in research.md R5 (`Session: required` metadata on `api-identity`); `/health/live` and `/health/ready`; Data Protection per research.md R10; `TimeProvider.System` registered
- [X] T008 [P] `Gateway.Tests/Fakes/SpaStub.cs`: Kestrel on `127.0.0.1:0`, echoes the path, query and headers it received as JSON
- [X] T009 `Gateway.Tests/Fakes/FakeIdentity.cs` per research.md R12: discovery and JWKS for issuer `https://gateway.test/`; `authorize` that checks client, `S256`, scopes, records every parameter, signs in a configurable user (sub, user_type, tenant_id, email in the identity token) and returns via `form_post`; `token` for the code grant (PKCE verified, client secret checked) and the refresh grant (rotating, counted, switchable to refuse with `invalid_grant` or fail with 500); `endsession` that records `id_token_hint` and redirects to `post_logout_redirect_uri` with `state`; an echo endpoint for any other path
- [X] T010 `Gateway.Tests/Fakes/Browser.cs`: one `CookieContainer`; follows `302`; auto-posts `form_post` forms; sends `https://gateway.test` URLs to the TestServer client. `Gateway.Tests/GatewayApp.cs`: `WebApplicationFactory<Program>` in environment `Testing`, base address `https://gateway.test`, clusters pointed at FakeIdentity and two SpaStubs, `IdentityBackchannelHandler` target set to FakeIdentity, and a `FakeTimeProvider` replacing `TimeProvider`
- [X] T011 `Gateway.Api/Sessions/IdentityBackchannelHandler.cs` per research.md R3, registered as the named `HttpClient` "identity". Tests first in `Gateway.Tests/BackchannelTests.cs`: a request for the public origin reaches the internal address with `X-Forwarded-Proto` and `X-Forwarded-Host` set to the public values; other hosts are not touched

---

## Phase 3: User Story 1 - Staff sign in to the backoffice (P1) 🎯 MVP

**Goal**: backoffice sign-in through the gateway, with only the session cookie in the browser.

**Independent Test**: spec.md US1.

- [X] T012 [US1] Tests first in `Gateway.Tests/SignInTests.cs`:
  - the login redirect carries `client_id=backoffice-bff`, `response_type=code`, `response_mode=form_post`, `S256` and the scopes in contracts/bff.md
  - the full flow sets only `__Host-Http-bff-backoffice` (the test origin is `https`), which is `HttpOnly`, `Secure`, `SameSite=Strict` and `Path=/`; the correlation and nonce cookies are `SameSite=Lax`
  - with an `http` `PublicOrigin` (a second `GatewayApp` configuration), the login redirect works (no HTTPS-metadata exception) and the cookie is `bff-backoffice`
  - `/bff/backoffice/user` returns the body in contracts/bff.md, and no response in the flow contains a token
  - `/bff/backoffice/user` without a session is `401` `session-required`, not a redirect
  - a foreign `returnUrl`, a `//` path or a path outside `/backoffice/` lands on `/backoffice/`
  - a refused sign-in redirects to `/backoffice/?signin-error=access_denied` with no cookie
  - a replayed callback creates no session
  - a tampered cookie counts as no session

  See them fail.
- [X] T013 [P] [US1] `Gateway.Api/Sessions/ReturnPaths.cs` per research.md R9, with unit tests `ReturnPathTests` in `Gateway.Tests/UnitTests.cs`
- [X] T014 [US1] `Gateway.Api/Sessions/SessionSetup.cs`: cookie and OIDC schemes for the backoffice per research.md R2, with cookie options from data-model.md App session, `SlidingExpiration = false` (research.md R8), `CorrelationCookie.SameSite` and `NonceCookie.SameSite` set to `Lax`, and `RequireHttpsMetadata` from `PublicOrigin`'s scheme; `OnTicketReceived` adds `bff_sid` (data-model.md App session `bff_sid`); `OnRemoteFailure` per research.md R9; cookie-scheme redirect events answer problem details
- [X] T015 [US1] `Gateway.Api/Bff/BffEndpoints.cs`: `GET /bff/backoffice/login` and `GET /bff/backoffice/user` per contracts/bff.md. T012 green

---

## Phase 4: User Story 2 - Shoppers sign in to a tenant's storefront (P1)

**Goal**: storefront sign-in per shop, with `tenant=<slug>` sent to Identity and one storefront session.

**Independent Test**: spec.md US2.

- [X] T016 [US2] Tests first in `Gateway.Tests/StorefrontSignInTests.cs`:
  - the login redirect for `acme` uses `storefront-bff` and `tenant=acme`
  - the flow sets the storefront session cookie and lands under `/shop/acme/`
  - `/bff/shop/acme/user` includes `"slug": "acme"`, and `/bff/shop/globex/user` is 401
  - signing in to `globex` replaces the acme session
  - a malformed slug is 404 and FakeIdentity records no request
  - backoffice and storefront sessions in one browser don't see each other

  See them fail.
- [X] T017 [P] [US2] Slug route constraint in `Gateway.Api/Sessions/ReturnPaths.cs` (research.md R2 slug format), with unit tests `SlugTests` in `Gateway.Tests/UnitTests.cs` using the same accepted and rejected examples as Identity's slug tests
- [X] T018 [US2] `SessionSetup.cs`: storefront schemes; `OnRedirectToIdentityProvider` adds `tenant` from `Items["slug"]`; `OnTicketReceived` keeps `slug` (data-model.md App session `slug`). `BffEndpoints.cs`: `/bff/shop/{slug}/login` and `/bff/shop/{slug}/user`. T016 green

---

## Phase 5: User Story 3 - SPAs call service APIs with the session (P1)

**Goal**: `/api/**` swaps the session for a bearer token, behind the anti-forgery header.

**Independent Test**: spec.md US3.

- [X] T019 [US3] Tests first in `Gateway.Tests/ApiForwardingTests.cs`:
  - a signed-in call to `/api/identity/users?x=1` reaches the echo as `/users?x=1` with the session's bearer token, no `Cookie`, no browser `Authorization` and no `X-Bff-App`
  - no session gives `401` `session-required`, and nothing reaches the echo
  - a missing or unknown `X-Bff-App` gives `400` `app-header-required`
  - `Sec-Fetch-Site: cross-site` gives `403` `cross-site-request`
  - echo statuses 400/401/403/404/409/422 with problem bodies pass through unchanged
  - `/api/unknown/x` is 404
  - with both sessions, `X-Bff-App: storefront` forwards the storefront token
  - an unreachable upstream gives `502` `upstream-unavailable`
  - `traceparent` reaches the echo

  See them fail.
- [X] T020 [P] [US3] `Gateway.Api/Proxy/ProxyTransforms.cs` per research.md R6: API routes drop `Cookie`, `Authorization` and `X-Bff-App`, then set the bearer token; other routes drop only the gateway's own cookies
- [X] T021 [P] [US3] `Gateway.Api/Proxy/ForwarderErrors.cs`: forwarder errors become `upstream-unavailable` problems (research.md R5)
- [X] T022 [US3] `Gateway.Api/Proxy/ApiSessionMiddleware.cs`: steps 1–3 and 6 of research.md R7, with a pass-through refresh until T025; wire it with `MapReverseProxy(proxy => { proxy.UseMiddleware<ForwarderErrors>(); proxy.UseMiddleware<ApiSessionMiddleware>(); })`. T019 green

---

## Phase 6: User Story 4 - Sessions refresh and end cleanly (P1)

**Goal**: one refresh per session, including for stale cookies; a refused refresh ends the session; sign-out ends it at Identity.

**Independent Test**: spec.md US4.

- [X] T023 [US4] Tests first in `Gateway.Tests/RefreshTests.cs`, using `FakeTimeProvider`:
  - before the threshold there is no refresh
  - past the threshold, one call refreshes, forwards the new token and returns an updated cookie
  - 10 parallel calls past the threshold make exactly 1 refresh and all succeed (SC-003)
  - a call with the pre-refresh cookie after the refresh reuses the result and sends no second refresh
  - a `/bff/backoffice/user` call with the pre-refresh cookie, past half the ticket lifetime, writes no `Set-Cookie`, so the old refresh token never returns to the browser
  - a refresh moves the ticket expiry to 12 hours from the refresh time
  - an already expired access token is refreshed
  - a refused refresh gives `401` `session-ended` and clears the cookie
  - a failing Identity gives `503` `identity-unavailable` and the session still works afterwards

  See them fail.
- [X] T024 [US4] Tests first in `Gateway.Tests/SignOutTests.cs`:
  - `logoutUrl` with the right `sid` clears the cookie, reaches `endsession` with the session's `id_token_hint` and the signed-out callback, and lands on `/backoffice/` (or `/shop/acme/`)
  - the old cookie is then 401 (needs T026a)
  - Identity received the hint and revoked the session's refresh tokens (FakeIdentity tracks the authorization behind each identity token)
  - a missing or wrong `sid` gives `400` `logout-proof-required` and the session stays
  - sign-out without a session lands on the start page

  See them fail.
- [X] T025 [US4] `Gateway.Api/Sessions/TokenRefresher.cs` per research.md R8 and data-model.md Refresh state, using the "identity" client and `TimeProvider`; `ApiSessionMiddleware` steps 4–5 of research.md R7, with `ExpiresUtc = now + 12h` on the rewritten ticket. T023 green
- [X] T026 [US4] `BffEndpoints.cs`: `/bff/backoffice/logout` and `/bff/shop/{slug}/logout` per contracts/bff.md, with a fixed-time `sid` comparison; signed-out callbacks redirect to the start page. T024 green
- [X] T026a [US4] `Gateway.Api/Sessions/SessionRevocations.cs` per research.md R14 and data-model.md Refresh state (Revoked session). Sign-out and a refused refresh revoke the `bff_sid` until the ticket expires, and the cookie scheme's `OnValidatePrincipal` rejects revoked sessions. Covered by `SignOutTests` (old cookie 401)

---

## Phase 7: User Story 5 - One address for the whole system (P1)

**Goal**: SPAs and Identity are reached through the gateway; Identity builds its addresses from forwarded headers.

**Independent Test**: spec.md US5.

- [X] T027 [US5] Tests first in `Gateway.Tests/RoutingTests.cs`:
  - the route table test: every route with an `/api/` prefix has `Session: required`, and every other route is in the reviewed list in contracts/routes.md
  - `/backoffice/a/b` and `/shop/acme/c` reach their SpaStub unchanged, without the gateway's cookies but with other cookies
  - `/connect/x`, `/account/css/identity.css` and `/.well-known/openid-configuration` reach FakeIdentity
  - client-sent `X-Forwarded-*` values are replaced
  - `/anything` is 404
  - `/health/ready` is 200 with every upstream down

  Implement anything missing in `Program.cs` and `appsettings.json` until green
- [X] T028 [P] [US5] Identity tests first in `src/services/identity/Identity.Tests/ForwardedHeadersTests.cs`:
  - from a known network, `X-Forwarded-Proto: https` and an allowed `X-Forwarded-Host` change the scheme and host the app sees
  - a forwarded host not in `AllowedHosts` is ignored
  - from an unknown address, or with no networks configured, the headers are ignored
  - known networks without allowed hosts stop the host at start

  Then implement `UseForwardedHeaders` per research.md R11 in `src/services/identity/Identity.Api/Program.cs`, with `ForwardedHeaders.KnownNetworks` and `ForwardedHeaders.AllowedHosts` in `src/services/identity/Identity.Api/Protocol/ProtocolSettings.cs` (data-model.md Identity changes `Identity:ForwardedHeaders:*`)
- [X] T029 [P] [US5] Identity tests first in `src/services/identity/Identity.Tests/Flow/IdentityTokenClaimsTests.cs`:
  - with the `email` scope, the identity token carries `sub`, `user_type`, `tenant_id` and `email`, while the access token carries only `sub`, `user_type` and `tenant_id`
  - without the `email` scope, there is no email claim
  - both clients may request `email`

  Then change `Protocol/Scopes.cs`, `Protocol/ClientRegistrar.cs` and `Protocol/PrincipalFactory.cs` (data-model.md Identity changes, research.md R4)
- [X] T030 [P] [US5] Move `src/services/identity/Identity.Api/wwwroot/css/identity.css` to `wwwroot/account/css/identity.css` and update `Pages/Shared/_Layout.cshtml`. Extend an existing login-page test to fetch the stylesheet at `/account/css/identity.css`
- [X] T031 [US5] `src/services/identity/compose.yaml`: remove `ports:`; post-logout URIs per data-model.md Identity changes (Client registration); `Identity__ForwardedHeaders__KnownNetworks__0..2` and `Identity__ForwardedHeaders__AllowedHosts__0` from new `dev.env` entries. `deploy/dev.env`: remove `IDENTITY_API_PORT`, add the forwarded networks and allowed host. Update `deploy/README.md` and `specs/014-identity-customers/quickstart.md` references to port 5101

---

## Phase 8: User Story 6 - Built, tested and shipped on its own (P2)

**Goal**: the gateway's own CI, and Identity CI that no longer needs a published port or the gateway.

**Independent Test**: spec.md US6.

- [X] T032 [US6] `scripts/dev.ps1`: optional `-Service <string[]>`, passed as service names to `up` (and `--wait` covers only them and their dependencies). Usage text updated
- [X] T033 [P] [US6] `tools/RepoChecks/PackagePins/PinCheck.cs` also checks `src/gateway/Directory.Packages.props`. Test first in `tools/RepoChecks.Tests/PackagePins/`
- [X] T034 [US6] `.github/workflows/gateway.yml` per research.md R13 (paths `src/gateway/**` and itself, `workflow_dispatch`, pinned action versions as in `identity.yml`, `if: always()` reset and cleanup check)
- [X] T035 [US6] `.github/workflows/identity.yml`: smoke start with `-Service identity-api`. Smoke checks run in the container: `docker exec inventory-and-order-identity-api-1 wget …` expecting 200 on `/health/ready` and 401 on `/roles`. The Static-mode log check stays
- [X] T036 [US6] `scripts/smoke-signin.ps1` per research.md R12, with its own cookie handling. It covers:
  - backoffice sign-in as the demo admin through the real login form (anti-forgery field included)
  - `/bff/backoffice/user`, then `/api/identity/users` with `X-Bff-App: backoffice` (200)
  - sign-out via `logoutUrl`, after which the old cookie gets 401
  - storefront registration of a random `@mail.test` customer on the demo shop, then `/bff/shop/demo/user`
  - the discovery document lists only the gateway origin
  - only the gateway publishes an application port

  It exits non-zero on the first failure
- [X] T037 [US6] `.github/workflows/deploy.yml`: restore the feed for identity, start `-Profile infra,apps`, run `scripts/smoke-signin.ps1`; `paths:` add `src/gateway/**`, `src/services/identity/**` and `scripts/smoke-signin.ps1`

---

## Phase 9: Polish

- [X] T038 Run quickstart.md sections 1–3 on the dev stack. Fix anything that fails, including a real browser at `http://localhost:8080` (unprefixed session cookies, sign-in round trip with `Lax` correlation cookies)
- [X] T039 [P] Review all changed files for comments, attribution and unpinned images: `dotnet run --project tools/RepoChecks -- images` and `-- packages`
- [X] T040 [P] Record implementation findings in `specs/015-api-gateway/research.md` (a new R14) for later features

---

## Dependencies

- **Setup → Foundational → stories.** T007 needs T004–T006. T010 needs T008, T009 and T011.
- **Stories:**
  - US1 comes first (MVP).
  - US2 needs US1's `SessionSetup` and `BffEndpoints`.
  - US3 needs US1 (a session to forward).
  - US4 needs US3 (`ApiSessionMiddleware`) and US1.
  - US5's gateway part (T027) needs T007 only. Its Identity tasks (T028–T031) are independent of the gateway stories.
  - US6 needs T031 (no Identity port) before T035–T037.
- **Polish** comes last.

## Parallel Examples

- After T001: T002 and T003.
- In Foundational: T005, T006 and T008.
- During US1–US4: T028, T029 and T030 (Identity folder).
- In US3: T020 and T021. In US6: T033 alongside T032.

## Implementation Strategy

1. Do Setup and Foundational, then US1: backoffice sign-in works against FakeIdentity. That is the MVP checkpoint.
2. Add US2, then US3, then US4, each ending green.
3. Add US5's Identity changes, then US6 CI, then run the full-stack smoke (T036–T038) last, because it needs everything.
