# Research: Identity Authentication

## R1. Libraries and versions

- **Decision**: Pin `OpenIddict.AspNetCore` and `OpenIddict.EntityFrameworkCore` 7.9.0 (latest stable), plus `Microsoft.AspNetCore.Identity.EntityFrameworkCore` and `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.15 (matching the other ASP.NET packages). Identity only, in `src/services/identity/Directory.Packages.props`.
- **Rationale**: OpenIddict was locked as the server in decision C1. Version 7.x supports .NET 10, the end-session endpoint naming, and refresh-token reuse detection.

## R2. One DbContext for users, protocol data and outbox

- **Problem**: ASP.NET Identity's stores normally want a context that derives from `IdentityDbContext`, but our `IdentityDbContext` derives from the BuildingBlocks `TenantDbContext`, which the outbox and tenant filter rely on.
- **Decision**:
  - Keep the single `IdentityDbContext : TenantDbContext`. Register `UserOnlyStore<AppUser, IdentityDbContext, Guid>`, whose only constraint is `DbContext`, and configure the user, claim, login and token entities in `OnModelCreating` with the same keys and indexes as `IdentityUserContext` (unique `NormalizedEmail` and `NormalizedUserName`, a concurrency stamp).
  - Tables are prefixed `identity_`. OpenIddict's entities join through `options.UseOpenIddict<Guid>()` on the context options.
  - Data Protection keys are stored in the same context through `IDataProtectionKeyContext`.
- **Rationale**: One database and one transaction, outbox included, as principle I requires. Roles aren't stored as Identity roles: Identity's own permissions stay the fixed catalogue and assignments from 011.
- **Alternatives considered**: A second DbContext for users. Rejected because it splits transactions with the outbox.

## R3. User model and email uniqueness

- **Decision**:
  - `AppUser : IdentityUser<Guid>` adds `UserType` (stored as text: `staff`, `customer`, `platform`) and `TenantId` (nullable).
  - A check constraint enforces that a platform user has no tenant and every other user has one.
  - Users are **not** tenant-filtered, because sign-in looks users up before any tenant is known.
  - Email is globally unique in 012.
- **Open for 013**: storefront customers may need the same email in several tenants (decision C6, tenant slug at sign-in). 013 decides whether uniqueness becomes per tenant for customers. It is recorded here so it isn't forgotten.

## R4. OpenIddict server configuration

- **Endpoints**:
  - authorization `/connect/authorize`
  - token `/connect/token`
  - end-session `/connect/endsession`
  - discovery and key set at `/.well-known/openid-configuration` and `/.well-known/jwks`
- **Flows**: only the code flow (`AllowAuthorizationCodeFlow`) and the refresh-token flow (`AllowRefreshTokenFlow`), with `RequireProofKeyForCodeExchange()`. No implicit, password or client-credentials flows, and every client is confidential (FR-005).
- **Lifetimes and refresh**:
  - Access token 15 minutes. Refresh token 12 hours, rolling, so active users stay signed in for a working day.
  - `SetRefreshTokenReuseLeeway(TimeSpan.Zero)`: a reused refresh token is refused and its authorization's tokens are revoked (FR-008, SC-003).
- **Format**: `DisableAccessTokenEncryption()`, so access tokens are signed JWTs (`at+jwt`). Codes and refresh tokens stay encrypted, because they are opaque to everyone but Identity.
- **Issuer**: set explicitly from `Identity:Issuer` (dev: `http://localhost:8080/`, the future gateway origin). Other services fetch metadata from the internal address and validate against the issuer it reports.
- **Scopes**:
  - `openid` and `offline_access`, plus the API scopes `identity`, `product`, `inventory`, `order` and `payment`, each registered with resource = its own name.
  - The audience is set from `scopeManager.ListResourcesAsync(grantedScopes)`.
  - A request without at least one API scope is rejected (FR-007).
- **Claims**:
  - `sub`, `user_type` and, for non-platform users, `tenant_id`, all with destination `access_token`. `sub` also goes into the identity token.
  - Nothing else goes into the access token.

## R4a. HTTPS requirement (analysis U1)

- **Problem**: OpenIddict refuses plain-HTTP requests outside Development. The dev stack runs over HTTP on loopback, and in production the gateway handles TLS (014).
- **Decision**: Add the setting `Identity:RequireHttps`, default `true`. When it is `false` (`dev.env`, tests), call `DisableTransportSecurityRequirement()` on OpenIddict's ASP.NET Core integration and set the Identity cookie's `SecurePolicy` to `SameAsRequest`. Production keeps the default. Feature 014 adds forwarded headers, so the gateway's `X-Forwarded-Proto: https` satisfies the check.

## R5. Protocol endpoints in passthrough mode

- **Decision**: `EnableAuthorizationEndpointPassthrough`, `EnableTokenEndpointPassthrough` and `EnableEndSessionEndpointPassthrough`, implemented as minimal endpoints in `Identity.Api/Connect/`:
  - **Authorize**: authenticate with the Identity cookie scheme. If there is no session, challenge, which redirects to `/account/login?ReturnUrl=…`. Otherwise load the user, build the principal (R4 claims, scopes, audience) and sign in with the OpenIddict server scheme. No consent screen: both clients are first-party with implicit consent.
  - **Token**:
    - Authorization-code grant: take the principal from the code.
    - Refresh grant: reload the user, and refuse if the user is missing, locked out, or the security stamp claim no longer matches the current stamp (FR-008).
    - Then rebuild the principal from the current user data.
  - **End session**: GET shows the confirmation page. POST signs out the Identity cookie and signs out through OpenIddict, which redirects only to registered post-logout URIs (FR-009). Revoking the user's tokens for that client uses `IOpenIddictTokenManager`.
- **Constitution III deviation**: these endpoints and the Razor pages use HttpContext sign-in and challenge APIs directly, which Wolverine handlers can't do. They contain authentication only, no business rules. This is recorded in Complexity Tracking, and the v1.4.0 amendment states the exception (R12).

## R6. The login page

- **Decision**: Razor Pages `Pages/Account/Login`, `Pages/Account/Logout` (the confirmation) and `Pages/Account/SignedIn`, plus `wwwroot/css/identity.css`: a centered card, Roboto from Google Fonts with system fallback, Material-like inputs and buttons, responsive.
  - `SignInManager.PasswordSignInAsync(..., lockoutOnFailure: true)`.
  - The model state message is one generic text for an unknown email and a wrong password. `IsLockedOut` gives the lockout text.
  - `ReturnUrl` is honoured only if `Url.IsLocalUrl`, otherwise it goes to `SignedIn`.
  - Antiforgery is the Razor Pages default.
  - The pages, the static files and the connect endpoints are allow-listed (`AllowAnonymous`), satisfying FR-022.
- **Login cookie**: ASP.NET Identity's application cookie: `HttpOnly`, `Secure` outside Development, `SameSite=Lax` (the redirect back from authorize must carry it). The path is `/`, behind the gateway's single origin.
- **Rationale**: A small server-rendered page is enough, and decision 2 ruled out Angular here.

## R7. Password and lockout

- **Decision**: `Password.RequiredLength = 8`, other password options at their defaults. `Lockout.MaxFailedAccessAttempts = 5`, `Lockout.DefaultLockoutTimeSpan = 10 min`, `Lockout.AllowedForNewUsers = true`. `RequireUniqueEmail = true`.
- **Time in tests**: ASP.NET Identity's lockout uses the system clock (`Microsoft.Extensions.Identity.Core` 10 has no `TimeProvider` hook). Tests prove expiry by moving `LockoutEnd` into the past in the database, not by waiting 10 minutes.

## R8. Startup tasks and the platform-admin seed

- **Decision**: Extend 011's `MigrationRunner` into `StartupTasks`, which runs these steps in order and sets the ready flag only after all of them:
  1. migrate
  2. register the scopes
  3. register or update the two clients (`OpenIddictApplicationManager`, idempotent, secrets from config)
  4. seed the platform admin
- **Platform admin seed**: if no user with `UserType=platform` exists, create one from `Identity:PlatformAdmin:Email` and `Identity:PlatformAdmin:Password`. If either is missing, or the password fails validation, throw. .NET's default `BackgroundServiceExceptionBehavior.StopHost` stops the host, which gives "startup fails with a clear message" (FR-017). An existing admin is never touched.
- **Clients config**: `Identity:Clients:<id>:Secret`, `:RedirectUri`, `:PostLogoutRedirectUri`. There are no defaults in code (FR-012).

## R9. Signing and encryption keys

- **Decision**: `Identity:Keys:Mode` is one of:
  - `Ephemeral` (tests): `AddEphemeralSigningKey()` and `AddEphemeralEncryptionKey()`.
  - `Directory` (dev): at `Identity:Keys:Path`, load `signing.pfx` and `encryption.pfx`, or create them on first start as self-signed RSA 2048 certificates (`CertificateRequest`), protected by `Identity:Keys:Password`.
  - `Certificates` (production): load from `Identity:Keys:SigningCertificatePath` and `Identity:Keys:EncryptionCertificatePath` plus the password.
- A missing or invalid configuration fails startup (FR-019).
- **Volume**: compose adds `identity-keys:/keys`. The Dockerfile creates `/keys` owned by `$APP_UID` before `USER`, so the named volume inherits a writable owner.
- **Rationale**: OpenIddict needs an encryption credential for codes and refresh tokens even with access-token encryption off.

## R10. Identity validating its own tokens: a BuildingBlocks 0.2.0 change

- **Problem**: The BuildingBlocks `tenant` and `platform` policies are built for the JwtBearer scheme. Identity validates with OpenIddict's validation scheme (`UseLocalServer`).
- **Decision**: BuildingBlocks 0.2.0 (MINOR, additive):
  - Split `AddBuildingBlocksAuthentication` into `AddBuildingBlocksRouteAuthorization(string authenticationScheme)` (policies, default and fallback, the route-category validator) and the JwtBearer part, which calls it with `JwtBearerDefaults.AuthenticationScheme`. Existing callers are unchanged.
  - `TestTokens` sets the JWT header `typ` to `at+jwt`. OpenIddict validation requires it, and JwtBearer accepts it.
  - Released as `building-blocks/v0.2.0`, and Identity pins 0.2.0.
- **Identity**:
  - `AddOpenIddict().AddValidation(o => { o.UseLocalServer(); o.AddAudiences("identity"); o.UseAspNetCore(); })`, with the default authenticate scheme set to OpenIddict validation.
  - `AddBuildingBlocksRouteAuthorization(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)`.
  - The pipeline stays `UseBuildingBlocksAuthentication()`.
- **Tests**: The test configuration sets the issuer to `TestTokens.Issuer` and adds `TestTokens.Key` as an extra signing key, so `TestTokens` tokens validate locally. The full-flow test proves that real tokens work too.
- **Verify in implement**: whether OpenIddict validation accepts self-contained JWTs it didn't store (token storage validation is off by default for access tokens). If it doesn't, the tests mint tokens through the real flow.

## R11. Data Protection

- **Decision**: `AddDataProtection().PersistKeysToDbContext<IdentityDbContext>().SetApplicationName("identity")`.
- **Rationale**: The login cookie and antiforgery tokens must survive restarts and work across several instances (011 proved two instances can run).

## R12. Constitution v1.4.0

- **Decision**: Run `/speckit-constitution` for a MINOR amendment:
  1. The SPA-auth row and text become the BFF (US8). Make clear that `SameSite=Strict` applies to the gateway's session cookie, while Identity's own login cookie is `Lax`, because it must survive the redirect back from sign-in (analysis A2).
  2. Principle III gets one sentence: Identity's protocol endpoints and server-rendered sign-in pages may use the framework's sign-in, sign-out and challenge APIs directly, and contain no business rules.

## R13. Tests

- **Full-flow test**: a `WebApplicationFactory` client with cookies kept and redirects off. The test:
  1. GETs `/connect/authorize` (client `backoffice-bff`, test redirect `https://gateway.test/bff/backoffice/signin-oidc`, PKCE S256, scopes `openid offline_access identity order`)
  2. follows to the login page, reads the antiforgery token and POSTs the credentials
  3. follows back to authorize, takes the `code` from the `Location` header
  4. POSTs to `/connect/token`
- **Further tests on that flow**: token claims and audience, refresh rotation, reuse, security-stamp change, end session, redirect-URI and PKCE rejection.
- **Lockout tests**: through the login page (R7).
- **Seed tests**: an empty database with and without admin settings.
- **Key tests**: Directory mode, by writing to a temp folder and restarting the factory.

## R14. Review fixes before merge

- **Client and scope registration**: the existing client is read back into a descriptor (`PopulateAsync`) and compared with the wanted one: client type, consent type, redirect URIs, post-logout URIs, permissions, requirements, plus `ValidateClientSecretAsync`. Any difference triggers one `UpdateAsync`. Scopes compare their resources the same way.
- **Allow-list**: `AllowAnonymousToFolder("/Account")` and `AllowAnonymousToFolder("/Connect")` replace `MapRazorPages().AllowAnonymous()`, so a new page outside these folders falls under the fallback policy.
- **Concurrent refresh**: the atomic claim gives exactly one winner. A loser that reaches OpenIddict after the winner's claim is treated as reuse and revokes the chain, which can include the winner's new token. This is kept on purpose (standard theft response). Clients MUST serialize refreshes per session; the 014 gateway does. The test asserts exactly one winner and nothing about the winner's token.
- **Refresh claim**: EF Core `ExecuteUpdateAsync` on the OpenIddict token entity instead of raw SQL, equally atomic.
- **Sign-out scope**: with an `id_token_hint`, the hint's authorization id is taken from the principal OpenIddict validated, and only that authorization and its tokens are revoked. Without a hint, the client's tokens for the user; without either, nothing is revoked. Revoking every token of the subject is removed. The user comes from the login cookie or, when the cookie has expired (the gateway refreshes for up to 12h without touching it), from the hint; OpenIddict accepts expired hints at end-session.
- **Login session**: the application cookie gets `ExpireTimeSpan = 12h` sliding. Authorize uses `SignInManager.ValidateSecurityStampAsync` on the cookie principal; a stale stamp is treated as no session.
- **FR-019 in production**: `SigningKeys.Configure` gets the host environment and refuses `Ephemeral` and `Directory` in Production. The dev compose sets `ASPNETCORE_ENVIRONMENT=Development`.
- **AppUser**: a private constructor for EF Core; `ForTenant` requires a tenant and a `staff` or `customer` type.
- **Tests**: negative tests assert the status; both races assert exactly one winner; the 5-second timing moves to quickstart.md; the signature test runs without the extra test signing key; `QueryHelpers` parses redirect queries.
- **Deferred**: forwarded headers and serialized refreshes (014); user type per client and email uniqueness (013); `prompt`/`max_age` and narrower refresh scopes (when a client needs them); login rate limiting, token pruning, certificate rotation, Data Protection key encryption at rest (phase 5).
