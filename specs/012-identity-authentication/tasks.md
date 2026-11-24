---

description: "Task list for feature 012 Identity Authentication"
---

# Tasks: Identity Authentication

**Input**: Design documents from `specs/012-identity-authentication/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. The spec's independent tests are automated integration tests.

**Global rules**:
- No comments (C#, cshtml, css, yaml).
- Pinned versions.
- No secrets in code.
- Testcontainers only.
- Build green after every task.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: BuildingBlocks 0.2.0 (blocks US7 and the tests)

- [X] T001 In `src/building-blocks/BuildingBlocks.AspNetCore/Authentication/AuthenticationExtensions.cs`, extract `AddBuildingBlocksRouteAuthorization(string authenticationScheme)`: the policies, the default and fallback policies, and `RouteCategoryValidator`. `AddBuildingBlocksAuthentication` calls it with `JwtBearerDefaults.AuthenticationScheme`, with no behaviour change (contracts/endpoints.md, research.md R10)
- [X] T002 [P] In `src/building-blocks/BuildingBlocks.Testing/Tokens/TestTokens.cs`, set the token header `typ` to `at+jwt` (research.md R10)
- [X] T003 [P] Add a test in `BuildingBlocks.AspNetCore.Tests/Authentication/` for a custom authentication scheme: a header-based test scheme plus `AddBuildingBlocksRouteAuthorization("custom")` gives the same 401/403/200 matrix on tenant, platform and open routes
- [X] T004 Run the full BuildingBlocks tests, `pack.ps1`, the consumer and analyzer checks, and the cleanup check. Commit. After the owner confirms, tag `building-blocks/v0.2.0` (local first; pushed with the PR, never moved once pushed)

---

## Phase 2: Foundational (Identity)

- [X] T005 Update `src/services/identity/Directory.Packages.props`: BuildingBlocks `0.2.0`, OpenIddict.AspNetCore and OpenIddict.EntityFrameworkCore `7.9.0`, Microsoft.AspNetCore.Identity.EntityFrameworkCore and Microsoft.AspNetCore.DataProtection.EntityFrameworkCore `10.0.15`. Reference them from `Identity.Api.csproj`, then run `restore-feed.ps1`
- [X] T006 Create `Identity.Api/Users/AppUser.cs` (`IdentityUser<Guid>`, plus `UserType` and `TenantId` per data-model.md "AppUser.UserType" and "AppUser.TenantId") and `UserTypes.cs`. Extend `IdentityDbContext`:
  - the user, claim, login and token entities configured as in data-model.md "AppUser": `identity_*` tables, unique `NormalizedEmail` and `NormalizedUserName`, the check constraint on `UserType` and `TenantId`
  - OpenIddict's entities mapped to the `openiddict_applications`, `openiddict_authorizations`, `openiddict_scopes` and `openiddict_tokens` tables (analysis U2)
  - `IDataProtectionKeyContext`
  - `UseOpenIddict<Guid>()` on the context options

  Add the migration `Authentication` (research.md R2, R3, R11)
- [X] T007 Create `Identity.Api/Protocol/ProtocolSettings.cs`, bound from `Identity` and validated on start (issuer, `RequireHttps` with default true, keys mode, client settings; the admin settings are validated by the seeder), and `SigningKeys.cs`, with the Ephemeral, Directory and Certificates modes, creating self-signed RSA 2048 PFX files in Directory mode (research.md R9, data-model.md "Configuration")
- [X] T008 Wire `Program.cs`:
  - `AddIdentityCore<AppUser>` with `UserOnlyStore<AppUser, IdentityDbContext, Guid>`, `SignInManager` and the Identity cookie; password and lockout per research.md R7
  - Data Protection to the database
  - the OpenIddict core (EF stores), the server (endpoints, flows, PKCE, lifetimes, zero reuse leeway, issuer, `DisableAccessTokenEncryption`, keys, passthrough) and validation (`UseLocalServer`, audience `identity`); the default scheme is OpenIddict validation
  - `AddBuildingBlocksRouteAuthorization(OpenIddict validation scheme)` instead of `AddBuildingBlocksAuthentication`
  - Razor Pages and static files
  - when `RequireHttps` is false: `DisableTransportSecurityRequirement()` and the cookie `SecurePolicy` set to `SameAsRequest` (research.md R4a)

  (research.md R4–R7, R10)
- [X] T009 Replace `MigrationRunner` with `Persistence/StartupTasks.cs`: migrate, register the scopes (`Protocol/Scopes.cs`, per data-model.md "Scope registrations"), run `ClientRegistrar` (idempotent create or update, per data-model.md "Client registrations"), then run `PlatformAdminSeeder` (FR-017), then set the ready flag. A failure in any step stops the host (research.md R8)
- [X] T010 Update `Identity.Tests/IdentityAppFixture.cs`:
  - the settings: issuer `TestTokens.Issuer`, `RequireHttps` false, keys mode Ephemeral plus `TestTokens.Key` added as a signing key, clients with redirect `https://gateway.test/bff/backoffice/signin-oidc` and post-logout `https://gateway.test/backoffice/`, the test admin email and password
  - remove `AddTestTokenValidation`
  - a cookie-keeping, non-redirecting client factory

  Re-run the existing 011 tests and make them pass, including the no-roles route test (research.md R10 verification)

**Checkpoint**: the existing Identity tests pass on the new authentication stack.

---

## Phase 3: User Story 1 - Sign in, gateway receives tokens (P1) 🎯 MVP

- [X] T011 [US1] Create `Identity.Api/Protocol/PrincipalFactory.cs`: build the principal from an `AppUser` with `sub`, `user_type`, `tenant_id` (non-platform only) and the security stamp claim (kept out of the access token), the granted scopes, and the resources from `IOpenIddictScopeManager`. Destinations are access token only, plus `sub` in the identity token. Reject a request with no API scope (research.md R4)
- [X] T012 [US1] Create `Identity.Api/Connect/AuthorizeEndpoint.cs` (GET/POST `/connect/authorize`, allow-listed): authenticate with the Identity cookie, challenge to the login page when there is no session, otherwise sign in through OpenIddict with the principal factory (research.md R5)
- [X] T013 [US1] Create `Identity.Api/Connect/TokenEndpoint.cs` (POST `/connect/token`, allow-listed): the code grant reuses the principal; the refresh grant reloads the user and refuses on a missing user, lockout or stamp mismatch, then rebuilds the principal (research.md R5)
- [X] T014 [US1] Create the Razor login page `Pages/Account/Login.cshtml(.cs)`, `SignedIn.cshtml`, `Shared/_Layout.cshtml`, `_ViewImports.cshtml` and `wwwroot/css/identity.css`: Material-like and responsive, one generic error, lockout message, local return URLs only, antiforgery (research.md R6). The pages and static files are allow-listed
- [X] T015 [US1] Create `Identity.Tests/Flow/CodeFlow.cs`: a helper that runs authorize → login (reading the antiforgery token) → authorize → code → token, and returns the token response
- [X] T016 [US1] Add `Identity.Tests/Flow/CodeFlowTests.cs`:
  - the full flow returns access, refresh and id tokens in under 5s (SC-001)
  - redeeming a code twice is rejected
  - a wrong verifier is rejected
  - a wrong client secret is rejected
  - an unregistered redirect URI is rejected with no redirect
  - a request without PKCE is rejected
  - a request without an API scope is rejected
  - using the issued access token on `GET /roles` gives 403 for the platform admin (a tenant route)

---

## Phase 4: User Story 2 - Token contents and audience (P1)

- [X] T017 [P] [US2] Add `Identity.Tests/Flow/TokenTests.cs`:
  - the decoded access token has header `typ` `at+jwt`, is not encrypted, has `sub`, `user_type=platform` and no `tenant_id`
  - it has no role, permission, email or name claims
  - `exp - iat` is 900 seconds
  - the audience is exactly the requested API scopes (`identity`, `order`)
  - a token requested with only `order` gets 401 from `/roles`
  - the signature verifies against `/.well-known/jwks`

---

## Phase 5: User Story 3 - Refresh and sign-out (P1)

- [X] T018 [US3] Create `Identity.Api/Connect/EndSessionEndpoint.cs` (GET shows `Pages/Account/Logout`, POST signs out of the Identity cookie and OpenIddict, revokes the user's tokens for that client, and redirects to the registered post-logout URI) and `Pages/Account/Logout.cshtml(.cs)` (research.md R5)
- [X] T019 [P] [US3] Add `Identity.Tests/Flow/RefreshTests.cs`:
  - a refresh rotates the tokens
  - the old refresh token is refused
  - reusing a token revokes its chain (the newest refresh token also fails)
  - after `UpdateSecurityStampAsync` a refresh fails
  - a locked-out user's refresh fails
  - two concurrent refreshes give at most one success
- [X] T020 [P] [US3] Add `Identity.Tests/Flow/EndSessionTests.cs`:
  - GET shows the confirmation
  - POST redirects to the registered post-logout URI
  - after sign-out the refresh token fails
  - an unregistered post-logout URI never gets a redirect

---

## Phase 6: User Story 4 - Login page safety (P2)

- [X] T021 [P] [US4] Add `Identity.Tests/LoginPageTests.cs`:
  - an unknown email and a wrong password give an identical status and message
  - 5 failures, then the correct password, give the lockout message
  - after moving `LockoutEnd` into the past, sign-in works and the count is reset (research.md R7)
  - a post without the antiforgery token is rejected
  - an external `ReturnUrl` is ignored, landing on signed-in
  - a login with no `ReturnUrl` at all lands on signed-in (analysis C2)
  - the page has a viewport meta and no SPA framework

---

## Phase 7: User Story 5 - Platform admin seed (P2)

- [X] T022 [P] [US5] Add `Identity.Tests/PlatformAdminSeedTests.cs`:
  - an empty database with the settings creates a platform admin with no tenant who can complete the code flow
  - an empty database without the settings stops the host, with a message naming the missing keys
  - a password that breaks the policy stops the host
  - an existing admin is unchanged after a restart with different settings

  Add `Identity.Tests/ClientRegistrarTests.cs` (analysis C1): a restart with the same settings changes nothing; a changed redirect URI or secret is updated (the new secret works, the old one fails); there is still exactly one application per client id

---

## Phase 8: User Story 6 - Keys survive restarts (P2)

- [X] T023 [P] [US6] Add `Identity.Tests/SigningKeyTests.cs`:
  - Directory mode with a temp folder creates `signing.pfx` and `encryption.pfx`
  - a second app instance on the same folder publishes the same JWKS and accepts a token issued by the first
  - Certificates mode with missing paths fails startup
  - Ephemeral mode writes nothing

  Delete the temp folder in `finally`
- [X] T024 [US6] Update `src/services/identity/Dockerfile` (`/keys` owned by `$APP_UID`) and `compose.yaml` (volume `identity-keys:/keys`; environment: `Identity__Issuer`, `Identity__RequireHttps=false`, `Identity__Keys__*`, `Identity__Clients__*`, `Identity__PlatformAdmin__*`; remove `Authentication__Authority`). Add `IDENTITY_ISSUER`, `GATEWAY_ORIGIN`, `BACKOFFICE_BFF_SECRET`, `STOREFRONT_BFF_SECRET`, `PLATFORM_ADMIN_EMAIL`, `PLATFORM_ADMIN_PASSWORD` and `IDENTITY_KEYS_PASSWORD` (all dev-only values) to `deploy/dev.env`, and a note to `deploy/README.md`

---

## Phase 9: User Story 7 - Identity's own API (P2)

- [X] T025 [US7] Add `Identity.Tests/Flow/OwnTokenTests.cs`: a staff user created in the test with tenant `acme` and assigned `identity.administrator` gets a real token through the code flow, and `GET /roles` returns 200. Assert that the no-roles route report lists `/connect/*` and `/account/*` as skipped (allow-listed)

---

## Phase 10: User Story 8 - Constitution (P3)

- [X] T026 [US8] Run `/speckit-constitution` to amend to v1.4.0 per research.md R12: the BFF replaces SPA code + PKCE, and principle III gets the narrow exception for protocol and sign-in pages

---

## Phase 11: Polish

- [X] T027 Run `./scripts/codegen.ps1 -Service identity` and commit any changes. Run quickstart.md sections 2–4 on the dev stack, then `dev.ps1 reset`
- [X] T028 Run all Identity, BuildingBlocks and RepoChecks tests, both pin checks and the cleanup check. Scan for comments in the new `.cs`, `.cshtml` and `.css` files
- [X] T029 Commit. After the owner confirms, push the branch and the tag `building-blocks/v0.2.0`, open the PR, and confirm `identity.yml`, `building-blocks.yml` and `deploy.yml` pass

---

## Phase 12: Review fixes (research.md R14)

- [X] T030 `Protocol/ClientRegistrar.cs` and `Protocol/Scopes.cs`: compare the full descriptor and update on any change (FR-011). Tests in `ClientRegistrarTests.cs`: a removed permission and a changed scope resource are restored on restart
- [X] T031 `Program.cs`: allow-list the `/Account` and `/Connect` page folders instead of all pages (FR-022)
- [X] T032 `Connect/ConnectEndpoints.cs`: refresh claim with `ExecuteUpdateAsync`; authorize validates the cookie's security stamp. `Program.cs`: cookie `ExpireTimeSpan` 12h (FR-016a). Tests: a stamp change sends the next authorize to login; concurrent code and refresh races have exactly one winner
- [X] T033 `Pages/Connect/EndSession.cshtml.cs`: revoke by the `id_token_hint` authorization, else by client, never the whole subject (FR-009). Test: signing out one session leaves another session's refresh token working
- [X] T034 `Protocol/SigningKeys.cs`: Production accepts only `Certificates` (FR-019); `compose.yaml` sets `ASPNETCORE_ENVIRONMENT=Development`. Test: Production with `Ephemeral` stops startup
- [X] T035 `Users/AppUser.cs`: private constructor, `ForTenant` validates its arguments
- [X] T036 Test fixes: status assertions in `CodeFlowTests`, no timing assert, signature test without the test key, `QueryHelpers` in `CodeFlow.cs`, tidy `ClientRegistrarTests` concurrency read
- [X] T038 `Pages/Connect/EndSession.cshtml.cs`: take the user from the `id_token_hint` when the login cookie is gone (FR-009). Test: sign-out with no cookie and an expired hint revokes the refresh token
- [ ] T037 Run the Identity suite, `codegen.ps1 -Service identity`, the cleanup check; commit; push after the owner confirms

---

## Dependencies

- Phase 1 (BuildingBlocks 0.2.0) → T005
- T005 → T006 → T007 → T008 → T009 → T010
- US1 (T011–T016) after T010. US2, US3, US7 after US1 (they use `CodeFlow`)
- US4 needs T014. US5 needs T009. US6: T023 needs T007, T024 is independent of the tests
- T026 is independent. The polish phase comes last

## Parallel Opportunities

- T002 ∥ T003
- After US1: T017 ∥ T019 ∥ T020 ∥ T021 ∥ T022 ∥ T023 (separate test files)

## Implementation Strategy

- MVP: Phases 1–3. The code flow works end to end in tests.
- Then token assertions, refresh and sign-out, login safety, the seed, keys, own validation, the constitution.
- One PR. Two owner confirmations: the 0.2.0 tag, then the push and PR.
