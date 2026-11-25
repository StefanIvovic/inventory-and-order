# Implementation Plan: API Gateway

**Branch**: `015-api-gateway` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/015-api-gateway/spec.md`

## Summary

A YARP gateway in `src/gateway/` becomes the single entry point and the backend for frontend of both SPAs. The plan:
- Four authentication schemes: a cookie and an OpenID Connect scheme per app, as confidential clients `backoffice-bff` and `storefront-bff` (R2).
- The OIDC back channel goes straight to Identity's internal address while the issuer stays public (R3).
- Routes come from configuration. Only `/api/**` routes need a session (R5).
- A proxy pipeline step checks the `X-Bff-App` header and the session, refreshes the tokens once per session (also for stale cookies), and swaps the cookie for a bearer token (R6–R8).
- `/bff/<app>/{login,user,logout}` endpoints. Sign-out is proven by a per-session random value and sends `id_token_hint` (R9).
- Identity changes, all carried over from 012:
  - forwarded headers from private networks only
  - user type, tenant and email in the identity token
  - its stylesheet moves under `/account/`
  - its port is no longer published
  - new post-logout addresses
  - CI smoke checks run inside its container (R4, R5, R9, R11)
- Gateway tests run with an in-process fake Identity and SPA stub (R12). A scripted sign-in against the real Identity runs on the full dev stack in the `deploy` workflow.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401)

**Primary Dependencies**:
- Gateway: Yarp.ReverseProxy 2.3.0 and Microsoft.AspNetCore.Authentication.OpenIdConnect 10.0.15.
- Gateway tests: Microsoft.AspNetCore.Mvc.Testing 10.0.15, xunit.v3 4.0.3 and Microsoft.Extensions.TimeProvider.Testing (current 10.x).
- Identity: unchanged.

**Storage**: none in the gateway. Data Protection keys go on a file volume. Identity: no schema change.

**Testing**: xUnit v3; `WebApplicationFactory` plus in-process Kestrel fakes (FakeIdentity, SpaStub); `FakeTimeProvider`; Identity's existing Testcontainers suite for its changes; `scripts/smoke-signin.ps1` on the dev stack in CI

**Target Platform**: Linux containers, GitHub Actions

**Project Type**: Web service (reverse proxy and BFF)

**Performance Goals**: no measurable overhead target; one refresh per session per token lifetime

**Constraints**: no comments; tokens never in a browser-readable place; no CORS; one gateway instance (phase 5 for more)

**Scale/Scope**: 1 new deployable (about 12 source files), 6 routes, 6 BFF endpoints, 5 Identity changes, 1 new workflow, script changes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Own folder, own build context, own workflow on `src/gateway/**`. The Identity workflow starts only `identity-api` (`dev.ps1 -Service`), so it never builds the gateway. Identity changes deploy on their own and work without the gateway. `deploy.yml` also triggers on `src/gateway/**` and `src/services/identity/**`. It is the cross-service sign-in check (research.md R12), not a service workflow, so the per-service trigger rule doesn't apply to it |
| II. Shared code via packages | ✅ | The gateway references no shared packages (R1). The slug pattern is copied, not shared |
| III. Handlers hold logic | ⚠️ justified | The gateway has no Wolverine handlers. It holds no application logic or business rules, only proxying and the authentication protocol, the same category as Identity's sign-in exception. See Complexity Tracking |
| IV. Transactions and messaging | ✅ n/a | No database, no messages |
| V. Multi-tenancy | ✅ | The gateway does not resolve tenants. Services take the tenant from the token. The storefront slug is passed to Identity only as the `tenant` parameter |
| VI. Authorization | ⚠️ justified | The gateway checks only that a session exists (401) and never permissions. The constitution's request-flow row says "Gateway validates the JWT (401)". With the BFF, the gateway gets the tokens from Identity over its own back channel and validates the identity token at sign-in. It does not re-check access tokens; the services do. The no-roles 403 route test applies to services with permission checks. The gateway has none, so a route-table test instead proves every `/api/**` route requires a session and every other path is in the reviewed list or 404. The request-flow row should be reworded in the next amendment |
| VII. Tests isolated | ✅ | Gateway tests use no containers. Identity keeps Testcontainers. `smoke-signin.ps1` runs only on the CI runner's dev stack, which is reset in `if: always()` |
| VIII. Docker | ✅ | Images from `deploy/versions.env` through build arguments. The gateway is in the `apps` profile |
| IX. No comments | ✅ | |
| X. No attribution | ✅ | |

**Gate: PASS**, with two justified deviations.

**Post-design re-check**: PASS. The design adds no new deviation. The constitution amendment (PATCH, request-flow wording plus the 011 codegen and Respawn notes from earlier features) is a separate `/speckit-constitution` change, not part of this PR.

## Project Structure

### Documentation (this feature)

```text
specs/015-api-gateway/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── bff.md            /bff endpoints, the anti-forgery header, problem types
│   └── routes.md         routing table, forwarded headers, Identity changes
└── tasks.md
```

### Source Code

```text
src/gateway/
├── Gateway.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── nuget.config
├── Dockerfile
├── compose.yaml                         gateway, port ${GATEWAY_PORT}, gateway-keys volume, profile apps
├── Gateway.Api/
│   ├── Program.cs
│   ├── appsettings.json                 ReverseProxy routes, Gateway defaults
│   ├── Configuration/GatewaySettings.cs public origin, Identity internal address, clients, refresh threshold, keys path
│   ├── Sessions/BffApp.cs               the two apps: schemes, cookie names, areas, callback paths
│   ├── Sessions/SessionTokens.cs        tokens in the ticket properties
│   ├── Sessions/SessionRevocations.cs   signed-out session ids (research.md R14)
│   ├── Sessions/SessionSetup.cs         cookie + OIDC schemes, events (tenant parameter, sid, failures)
│   ├── Sessions/IdentityBackchannelHandler.cs
│   ├── Sessions/TokenRefresher.cs
│   ├── Sessions/ReturnPaths.cs          local return path and slug rules
│   ├── Bff/BffEndpoints.cs              login, user, logout
│   ├── Proxy/ApiSessionMiddleware.cs    header, session, refresh, bearer
│   ├── Proxy/ProxyTransforms.cs         cookie and header stripping per route kind
│   ├── Proxy/ForwarderErrors.cs         502/504 → problem details
│   └── Problems.cs                      problem types and the traceId extension
└── Gateway.Tests/
    ├── Fakes/FakeIdentity.cs            OIDC provider, token counter, echo
    ├── Fakes/SpaStub.cs
    ├── Fakes/Browser.cs                 cookies, redirects, form_post
    ├── GatewayApp.cs                    WebApplicationFactory, FakeTimeProvider
    ├── SignInTests.cs                   US1, US2
    ├── ApiForwardingTests.cs            US3
    ├── RefreshTests.cs                  US4
    ├── SignOutTests.cs                  US4
    ├── RoutingTests.cs                  US5, route table
    └── ReturnPathTests.cs, SlugTests.cs

src/services/identity/
├── Identity.Api/Program.cs              UseForwardedHeaders, email scope
├── Identity.Api/Protocol/ProtocolSettings.cs   ForwardedHeaders.KnownNetworks
├── Identity.Api/Protocol/PrincipalFactory.cs   identity-token claims
├── Identity.Api/Protocol/Scopes.cs, ClientRegistrar.cs   email scope and permission
├── Identity.Api/wwwroot/account/css/identity.css    moved
├── Identity.Api/Pages/Shared/_Layout.cshtml         stylesheet path
├── Identity.Tests/                      forwarded headers, identity-token claims, email scope
└── compose.yaml                         no port, post-logout URIs, forwarded networks

deploy/compose.yaml, deploy/dev.env, deploy/README.md
scripts/dev.ps1 (-Service), scripts/smoke-signin.ps1 (new)
tools/RepoChecks/PackagePins/PinCheck.cs (+ src/gateway) and its test
.github/workflows/gateway.yml (new), identity.yml (smoke in container, -Service), deploy.yml (apps, sign-in smoke, paths)
InventoryAndOrder.slnx                   adds the gateway projects
```

**Structure Decision**: the gateway follows the service layout without `Contracts`, and groups by concern (`Sessions`, `Bff`, `Proxy`) because it has no use-case slices.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because | Approved |
|---|---|---|---|
| No Wolverine handlers in the gateway (III) | The gateway runs the OIDC protocol and proxying, which use the framework's challenge, sign-in and forwarder APIs directly. It has no application logic | Wrapping login or proxying in message handlers would add a message hop around framework calls that cannot run in a handler, the same reason as Identity's sign-in exception | Owner, 2026-11-25 |
| Gateway checks the session, not the access-token signature (VI request-flow row) | The gateway is the OIDC client that received the tokens from Identity over its own back channel, and it validated the identity token's signature at sign-in | Re-validating its own access tokens duplicates the services' validation and adds a JWKS dependency to every proxied call, with no attacker able to inject a token into the encrypted cookie | Owner, 2026-11-25 |
| A route-table test instead of the no-roles 403 test (VI, merge gates) | The gateway has no permissions to check | A 403 test would have nothing to assert. The route-table test guards the gateway's own boundary (session required on every API route) | Owner, 2026-11-25 |

Follow-up: constitution PATCH, as a separate `/speckit-constitution` change:
- reword the request-flow row to "Gateway checks the session (401)…"
- exempt the gateway from the no-roles test in favour of its route-table test
- fix the 011 codegen wording and the Respawn mention
