# Research: API Gateway

## R1. Project shape and packages

- **Decision**: `src/gateway/` with `Gateway.slnx`, `Gateway.Api`, `Gateway.Tests`, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config` (nuget.org only), `Dockerfile` and `compose.yaml`. Packages: `Yarp.ReverseProxy` 2.3.0, `Microsoft.AspNetCore.Authentication.OpenIdConnect` 10.0.15 (the cookie handler is in the shared framework), `Microsoft.AspNetCore.Mvc.Testing` 10.0.15 and `xunit.v3` 4.0.3 for tests. No Wolverine, no EF Core, no database.
- **Rationale**: the gateway has no domain, no messages and no data. Version 11.0.0 of the OpenIdConnect package targets .NET 11, so 10.0.15 matches the shared framework, as in Identity.
- **No BuildingBlocks**: `UseBuildingBlocks` installs the identity middleware, which reads `sub`/`tenant_id`/`user_type` from `HttpContext.User` and answers 401 `invalid-identity` for anything else. Cookie principals would trip it. The gateway needs only ASP.NET problem details, which it configures itself with the same `traceId` extension and short type slugs (`session-required`). With no BuildingBlocks pin, the Docker build needs no `packages` context and `restore-feed.ps1` has nothing to do.
- **Alternatives**: Duende BFF (paid, rejected in 011 planning); a BuildingBlocks gateway package (no second consumer, so it would be speculative).

## R2. Two apps, two OIDC schemes, two cookies

- **Decision**: four authentication schemes: `backoffice` (cookie) + `backoffice-oidc` and `storefront` (cookie) + `storefront-oidc`. Each OIDC scheme uses `ResponseType=code`, `UsePkce=true`, `ResponseMode=form_post`, `SaveTokens=true`, `GetClaimsFromUserInfoEndpoint=false`, `MapInboundClaims=false`, scopes `openid offline_access email identity product inventory order payment`, and the client secret from configuration. Callback paths are the registered `/bff/backoffice/signin-oidc` and `/bff/storefront/signin-oidc`. Signed-out callbacks are `/bff/backoffice/signout-callback-oidc` and `/bff/storefront/signout-callback-oidc`.
- **Cookies**: names `bff-backoffice` and `bff-storefront`, with the `__Host-Http-` prefix only when `PublicOrigin` is `https` (`__Host-Http-bff-backoffice`, the prefix the IETF browser-based-apps BCP suggests for BFF cookies). Browser support for `__Host-` cookies from `http://localhost` is not reliable, so the dev stack (plain HTTP) uses the bare names. Production (TLS at the gateway) gets the prefix, which forces `Secure`, `Path=/` and no `Domain`. Either way: `HttpOnly`, `SameSite=Strict`, `SecurePolicy=Always`, `Path=/`, and a browser-session cookie (not persistent). The ticket expiry is 12 hours, matching Identity's refresh-token lifetime, and it is extended only when a refresh rewrites the cookie (R8), not by sliding renewal. Chunking is the cookie handler's default `ChunkingCookieManager`.
- **Correlation and nonce cookies**: the OIDC handler's defaults are `SameSite=None`, `SecurePolicy=Always` (ASP.NET 10 docs for `RemoteAuthenticationOptions.CorrelationCookie` and `OpenIdConnectOptions.NonceCookie`). Both are set explicitly to `SameSite=Lax`. The whole sign-in round trip is same-site through the gateway, including Identity's `form_post` back to the callback, and these cookies live a few minutes and are bound to `state`.
- **HTTPS metadata**: `RequireHttpsMetadata` defaults to `true`, and the handler throws for an `http` authority. It is set to `PublicOrigin`'s scheme being `https`. So it is `false` only in the plain-HTTP dev stack, as with Identity's `RequireHttps`. A test runs the gateway with an `http` public origin and checks that sign-in starts.
- **No redirects for XHR**: the cookie schemes' `OnRedirectToLogin` and `OnRedirectToAccessDenied` answer 401/403 problem details. Only the login endpoints ever challenge the OIDC schemes.
- **Storefront slug**: the login endpoint puts the slug in `AuthenticationProperties.Items["slug"]`. `OnRedirectToIdentityProvider` adds `tenant=<slug>` to the authorization request. After sign-in the slug stays in the ticket's properties. A new storefront sign-in overwrites the one storefront cookie, which replaces the session.
- **Slug format**: the same rule as Identity's `Tenant.SlugPattern` (`^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$`: 3–63 lowercase letters, digits and single inner hyphens), checked by a route constraint, so a malformed slug is 404 before anything else. The gateway copies the pattern, because it cannot reference Identity's code; a test pins both to the same examples.
- **Alternatives**: one cookie with both sessions (one refresh or sign-out would touch the other app); a cookie per shop (multiplies cookies and refreshes; no use case).

## R3. Front channel vs back channel to Identity

- **Problem**: Identity's issuer is the public gateway address (`http://localhost:8080/` in dev). The browser must use it, but inside the gateway container that address is the gateway itself.
- **Decision**: `Authority` is the public issuer. The OIDC schemes and the token refresher share a named `HttpClient` "identity" whose `IdentityBackchannelHandler` rewrites any request for the public origin to Identity's internal address (`http://identity-api:8080`) and sets `X-Forwarded-Proto` and `X-Forwarded-Host` to the public values. The discovery document, JWKS, token endpoint and refresh calls then go straight to Identity. Identity sees the public origin, and issuer validation of the identity token stays exact.
- **Rationale**: one configured issuer everywhere; no hairpin through the gateway's own port; works the same behind TLS.
- **Alternatives**: `MetadataAddress` set to the internal address (the discovery document would still list public endpoint URLs); a hairpin through the gateway (extra hop, and the token request would go through the API pipeline).

## R4. What the user endpoint knows: identity-token claims

- **Finding**: Identity's `PrincipalFactory` sends only `sub` to the identity token. `user_type` and `tenant_id` go to the access token only, and no email claim exists.
- **Decision**: Identity registers the standard `email` scope (no resource) and grants it to both gateway clients. `PrincipalFactory` adds `user_type` and `tenant_id` to the identity token as well, and `email` to the identity token only, when the `email` scope was granted. Access tokens are unchanged (principle VI).
- **Rationale**: the gateway must not parse access tokens, which are meant for the services. The identity token is the client's own statement about the user.
- **A deliberate choice**: for the code flow, OpenID Connect Core returns `email` from the userinfo endpoint, not in the identity token. Putting it in the identity token is allowed but not the default. It is chosen here to avoid a userinfo endpoint and a back-channel call per sign-in. The identity token reaches only the gateway, never the browser.
- **Alternatives**: decoding the access token in the gateway (couples the gateway to the access-token format); a userinfo endpoint (one more Identity endpoint and a back-channel call per sign-in).

## R5. Routing table

- **Decision**: YARP routes come from configuration (`ReverseProxy` section), clusters from environment variables:

  | Route | Match | Cluster | Transform | Session |
  |---|---|---|---|---|
  | `identity-protocol` | `/connect/{**rest}` | identity | none | no |
  | `identity-account` | `/account/{**rest}` | identity | none | no |
  | `identity-discovery` | `/.well-known/{**rest}` | identity | none | no |
  | `backoffice-spa` | `/backoffice/{**rest}` | backoffice-spa | none | no |
  | `storefront-spa` | `/shop/{**rest}` | storefront-spa | none | no |
  | `api-identity` | `/api/identity/{**rest}` | identity | `PathRemovePrefix: /api/identity` | required |

  The `/bff/**` endpoints and `/health/*` are minimal API endpoints in the gateway. There is no catch-all route, so any other path is 404. A new service is one more `api-<service>` route and cluster in configuration (FR-003).
- **Session flag**: route metadata `Session: required` replaces the earlier "`spa` authorization policy" idea. An authorization policy cannot pick the cookie scheme from a request header, and it would answer with a challenge rather than our problem details. The proxy pipeline step (R7) reads the flag.
- **Identity's stylesheet**: Identity serves `/css/identity.css` at its root. It moves to `wwwroot/account/css/`, so it is served as `/account/css/identity.css` and covered by `identity-account`. No generic `/css` route on the gateway.
- **Upstream down**: YARP answers 502 when the connection fails and 504 on timeout. A `ForwarderError` hook turns that into problem details (`upstream-unavailable`). In the dev stack the SPA clusters point at `backoffice-spa:80` and `storefront-spa:80`, which don't exist until the SPA features, so DNS fails and the answer is 502. The spec allows 502 or 503.

## R6. Headers and cookies on forwarded requests

- **Decision**:
  - All routes: YARP's default `X-Forwarded-For/Proto/Host` in `Set` mode (client-sent values are replaced, never appended). Trace context (`traceparent`) flows through `HttpClient`'s built-in propagation, and the trace id is the correlation id. No separate header, which matches services that already log the trace id.
  - API routes: remove `Cookie` and any browser `Authorization`, then set `Authorization: Bearer <access token>` and remove `X-Bff-App`.
  - Identity and SPA routes: remove only the gateway's own cookies (the session cookies by their configured names, plus the OIDC correlation and nonce cookies) from `Cookie`. Identity keeps its `identity.session` login cookie.
- **Rationale**: services must never see a browser credential, and Identity must never see the gateway's sessions.

## R7. API pipeline step: anti-forgery, session, refresh, token

- **Decision**: one proxy pipeline middleware (`MapReverseProxy(proxy => proxy.UseApiSession())`) for routes with `Session: required`:
  1. `Sec-Fetch-Site`, when present, must be `same-origin`. Otherwise 403 `cross-site-request`.
  2. `X-Bff-App` must be `backoffice` or `storefront`. Otherwise 400 `app-header-required`. A cross-site page can only send a custom header after a CORS preflight, which the gateway never approves.
  3. `AuthenticateAsync(<app cookie scheme>)`. If there is no ticket, 401 `session-required`.
  4. Ask the `TokenRefresher` (R8) for current tokens. If Identity refuses, sign out the cookie and answer 401 `session-ended`. If Identity can't be reached, 503 `identity-unavailable`, and the cookie stays.
  5. If the tokens changed, `SignInAsync(<app cookie scheme>, principal, properties)` with the updated tokens. The response has not started yet, so `Set-Cookie` goes out with the proxied response.
  6. Store the access token for the transform, which writes the bearer header.
- **Status for a missing header**: 400. It is a malformed request, not an authentication problem, and 403 is reserved for permission failures that list missing permissions (principle VI).
- **Alternatives**: ASP.NET antiforgery tokens (need a token endpoint and SPA plumbing; the custom header plus `SameSite=Strict` plus no CORS is the standard BFF defence).

## R8. Refresh: one per session, stale cookies included

- **Decision**: a singleton `TokenRefresher`:
  - The session key is a random `bff_sid` (32 bytes, base64url) created at sign-in and stored in the ticket properties. It is also the sign-out proof (R9).
  - `GetCurrentAsync(sid, tokens)`: if `expires_at` is more than the threshold (default 1 minute, `Gateway:Refresh:Threshold`) ahead, return the tokens unchanged.
  - Otherwise look in a private `MemoryCache` under `(sid, SHA-256 of the refresh token)`. A hit means another request already refreshed with this refresh token, so return that result.
  - Otherwise take the session's `SemaphoreSlim` (from a `ConcurrentDictionary`, removed when idle), check the cache again, then call the token endpoint with `grant_type=refresh_token` and the client credentials. Cache the result for 5 minutes under the old refresh token's key and return it.
  - Identity's `400 invalid_grant` is a refusal and is cached as such for the same key, so racing callers all get 401. A transport error or 5xx is "unavailable" and is not cached.
- **Rationale**: a request that left the browser before the first refresh's `Set-Cookie` arrived carries the old refresh token. Without the keyed cache it would send a used refresh token, and Identity (012 R14) would revoke the whole chain.
- **No sliding renewal**: the cookie handler's `SlidingExpiration` re-issues the ticket it read when a request arrives past half its lifetime. On a request that carries a pre-refresh cookie (for example `/bff/.../user` racing a refresh), that would write the old, already-redeemed refresh token back over the browser's new cookie. Once the 5-minute result cache has expired, the next refresh would send a used token and Identity would revoke the chain. So `SlidingExpiration = false`. The only writer of a session cookie after sign-in is the refresh path, which sets `ExpiresUtc = now + 12h`, so active sessions still last, and the user endpoint never writes a cookie.
- **Scope**: one gateway instance. Phase 5 moves the lock and the cache to Redis.

## R9. Sign-in, user and sign-out endpoints

- **Login** `GET /bff/backoffice/login?returnUrl=…` and `GET /bff/shop/{slug}/login?returnUrl=…`: validate `returnUrl` (local, no `//` or `\`, and starting with `/backoffice/`, or `/shop/{slug}/` for that slug; otherwise the app's start page), then challenge the app's OIDC scheme with `RedirectUri = returnUrl`. On `OnTicketReceived`, add `bff_sid` and, for the storefront, `slug` to the ticket.
- **Sign-in failure**: `OnRemoteFailure` and an `error` from Identity redirect to `<app start>?signin-error=<code>` (`access_denied`, `invalid_request` or `server_error`), with no session and no loop.
- **User** `GET /bff/backoffice/user` and `GET /bff/shop/{slug}/user`: 401 `session-required` without a ticket or (storefront) for another slug. Otherwise `200` with the body in [contracts/bff.md](contracts/bff.md), built from the identity-token claims kept in the ticket.
- **Logout** `GET /bff/backoffice/logout?sid=…` and `GET /bff/shop/{slug}/logout?sid=…`:
  - No session: redirect to the app's start page.
  - `sid` missing or not equal to the session's `bff_sid` (fixed-time comparison): 400 `logout-proof-required`, and the session stays.
  - Otherwise revoke the session id (R14), sign out the OIDC scheme with `RedirectUri = <app start>` (it reads the `id_token` from the cookie, so it goes first), then sign out the cookie scheme. The OIDC handler sends `id_token_hint` from the saved tokens, `post_logout_redirect_uri` set to the signed-out callback, and `state`. Identity revokes that authorization only (012 R14) and returns to the callback, which redirects to the app's start page.
- **Identity change**: the registered post-logout URIs move to `${GATEWAY_ORIGIN}/bff/backoffice/signout-callback-oidc` and `${GATEWAY_ORIGIN}/bff/storefront/signout-callback-oidc`, a compose-only change that `ClientRegistrar` applies in place.

## R10. Data Protection keys

- **Decision**: `AddDataProtection().SetApplicationName("gateway")`. With `Gateway:Keys:Path` set, `PersistKeysToFileSystem(path)`. In dev compose that is `/keys` on the `gateway-keys` volume. Without it the keys are ephemeral, which tests use; outside Development and Testing the gateway refuses to start without a path, as Identity does for signing keys.
- **Cookie from an old key ring**: the cookie handler can't decrypt it and treats it as anonymous, so 401 (edge case in the spec).

## R11. Identity behind the gateway (012 carry-overs)

- **Forwarded headers**: `UseForwardedHeaders` first in Identity's pipeline, with `XForwardedFor | XForwardedProto | XForwardedHost`, `ForwardLimit = 1`, `KnownProxies` cleared, `KnownIPNetworks` from `Identity:ForwardedHeaders:KnownNetworks`, and `AllowedHosts` from `Identity:ForwardedHeaders:AllowedHosts` (dev: `localhost:8080`). An empty `AllowedHosts` allows every host (ASP.NET docs warn this lets an attacker spoof generated links), so Identity refuses to start with known networks but no allowed hosts. A forwarded host that is not allowed is ignored. Dev sets the Docker private ranges (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`). Identity no longer publishes a port, so only containers on the compose network can reach it, and the gateway always replaces the forwarded values (R6). Pinning one subnet was rejected: a fixed compose subnet would clash between the dev stack and the `e2e-test` project. Production gives the real gateway network. Without the setting no forwarded header is trusted.
- **Serialized refresh**: R8. **`id_token_hint`**: R9.
- **No published port**: `ports:` is removed from `identity-api` and `IDENTITY_API_PORT` from `dev.env`. Identity's CI smoke checks move inside the container (`docker exec inventory-and-order-identity-api-1 wget …`), and `dev.ps1` gets `-Service <name[]>`, so the Identity workflow starts only `identity-api` (with its dependencies) and never builds the gateway.

## R12. Testing without Identity

- **Decision**: the gateway runs in `WebApplicationFactory` (TestServer) at `https://gateway.test`. Two real Kestrel apps run on `127.0.0.1:0` inside the test process:
  - **FakeIdentity**: discovery, JWKS, `authorize` (signs in the configured test user at once, checks PKCE S256, records parameters such as `tenant`, and `form_post`s the code back), `token` (code and refresh grants, rotating refresh tokens, counting refreshes, can be set to refuse or fail), `endsession` (records `id_token_hint`, redirects to `post_logout_redirect_uri` with `state`), plus an echo endpoint that records the method, path, query and headers it receives. Identity tokens are signed with an RSA key from `Microsoft.IdentityModel.JsonWebTokens` (already a dependency of the OIDC package). Its issuer is `https://gateway.test/`.
  - **SpaStub**: answers every path with the path it received.
- **Test browser**: a small `Browser` helper with one `CookieContainer` that follows redirects and auto-submits `form_post` forms. Every `https://gateway.test` URL goes to the TestServer client, so the front channel really passes through the gateway's `/connect/**` route to FakeIdentity, as in production. The back channel goes through `IdentityBackchannelHandler` to FakeIdentity's real address.
- **Clock**: the gateway takes `TimeProvider` from DI, and tests use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) to cross the refresh threshold.
- **No containers**: no Testcontainers in gateway tests, so the cleanup check passes trivially.
- **Against real Identity**: `scripts/smoke-signin.ps1` runs in the `deploy` workflow on the full dev stack. .NET's `CookieContainer` won't send `Secure` cookies over `http://localhost`, so the script keeps cookies itself (reads `Set-Cookie`, sends `Cookie`). It covers:
  - backoffice sign-in as the demo admin, then `/bff/backoffice/user`, then `/api/identity/users` with `X-Bff-App`, then sign-out, after which the old cookie gets 401
  - storefront registration of a random customer on the demo shop, then `/bff/shop/demo/user`
  - Identity's discovery document lists only the gateway origin
  - exactly one application port is published
  The `deploy` workflow's `paths:` gain `src/gateway/**`, `src/services/identity/**` and the script, because this is the cross-service check (it is not a service workflow, so principle I's trigger rule does not apply).

## R13. Gateway CI and repository checks

- **Decision**: `.github/workflows/gateway.yml` (paths `src/gateway/**` and itself): `dotnet test`, the image-pin check, the package-pin check, a smoke start (`dev.ps1 up -Profile infra,apps -Service gateway`, see R14), smoke checks (`/health/ready` 200, `/bff/backoffice/user` 401, `/api/identity/users` with the header 401, `/backoffice/` 502), reset, and the cleanup check.
- `PinCheck` also scans `src/gateway/Directory.Packages.props`. The image rule already walks the whole repository for compose files and Dockerfiles (`SourceWalker`), so it covers the gateway unchanged.
- **Readiness**: `/health/ready` reports healthy once the host has started, and configuration is validated at startup (`ValidateOnStart`). No upstream checks (US6.3).

## R14. Implementation findings

Verified online before and during implementation, and by tests:
- IETF "OAuth 2.0 for Browser-Based Applications" (draft 27, published as RFC 10017), section 6.1:
  - The BFF MUST be a confidential client and MUST set `Secure` and `HttpOnly`. It SHOULD set `SameSite=Strict`, `Path=/`, no `Domain`, and a cookie-name prefix such as `__Host-Http-`.
  - When it relies on CORS for CSRF defence, every request MUST carry a static custom header.
  - The session lifetime equals the refresh token's maximum lifetime.
  - Outbound requests go only to explicitly allowed hosts and paths.
  - The design follows all of these. The prefix is used only on HTTPS origins (R2).
- **YARP 2.3 docs** (transforms, authn/authz):
  - `X-Forwarded-*` defaults to `Set`, which replaces client values.
  - The `Host` header is not copied, so the destination's host is used.
  - Cookies and `Authorization` are forwarded by default, which is why the API transform removes both.
- **Framework defaults** (ASP.NET 10 XML docs): correlation and nonce cookies default to `SameSite=None`; `RequireHttpsMetadata` defaults to `true`; an empty `ForwardedHeadersOptions.AllowedHosts` allows every host.

Found by the tests and the dev-stack smoke:
- **Sign-out did not revoke the cookie.** With tokens in the cookie, an old cookie stayed valid after sign-out (SC-005). `SessionRevocations` now keeps signed-out and refused session ids in memory until their ticket would expire, and the cookie scheme's `OnValidatePrincipal` rejects them. This holds for one instance. A gateway restart forgets the list, but the next refresh is then refused by Identity, so an old cookie lives at most one access-token lifetime. Phase 5 moves the list to Redis.
- **`AllowedHosts` matches the host only.** `ForwardedHeadersMiddleware` compares host patterns without the port, so `localhost:8080` never matched and the forwarded headers were silently ignored. Identity's discovery document then listed `http://identity-api:8080`. `ForwardedHeadersSettings.HostPatterns()` strips ports from the configured hosts. An IPv4-mapped IPv6 caller address (dual-stack Kestrel) is matched by IPv4 networks; a test pins this.
- **Not using forwarded headers at all when none are trusted.** With no known proxies or networks, `ForwardedHeadersMiddleware` trusts every caller. So Identity adds the middleware only when `KnownNetworks` is set.
- **Two independent guards against sliding renewal.** `SlidingExpiration = false` plus `AllowRefresh = false` on the ticket. A mutation test showed that either one alone keeps the stale-cookie test green, and both removed make it fail.
- **Session cookie size.** With identity, access and refresh tokens, the session is split into four cookies of about 4 KB each (`bff-backoffice`, `C1`–`C3`). That is accepted by Chromium, but it is sent on every request. The phase-5 server-side session store removes it.
- **Revocation at Identity after sign-out.** The gateway tests prove the hint reaches the end-session endpoint and that the fake revokes that authorization's refresh tokens. Identity's 012 `EndSessionTests` prove that the real Identity revokes by hint.
- **Real browser check.** Chromium accepts the `Secure` session cookies from `http://localhost` without the prefix. `document.cookie` is empty, and fetch calls with `X-Bff-App` succeed.
- **OpenIddict** advertises `form_post` and accepts it without a client permission. The identity token carries `email` once the `email` scope is registered and permitted.
- **Test fakes.** `MapFallback` skips paths that look like files (`{*path:nonfile}`), so the fakes use `MapFallback("{**path}", ...)`.
- **PowerShell.** A `[string]` parameter defaulting to `$null` becomes `''`; the smoke script uses explicit empty strings.
- **Compose.** `docker compose up <service>` with only the `apps` profile fails, because `identity-api` depends on `rabbitmq` in `infra`. Single-service starts pass `-Profile infra,apps -Service <name>`.
