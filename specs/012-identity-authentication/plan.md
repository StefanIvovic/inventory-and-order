# Implementation Plan: Identity Authentication

**Branch**: `012-identity-authentication` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/012-identity-authentication/spec.md`

## Summary

Identity becomes the token issuer. The plan:
- ASP.NET Core Identity users live in the existing `IdentityDbContext`, together with OpenIddict's entities and the Data Protection keys.
- OpenIddict 7.9 runs the code flow with PKCE and client secrets, rotating refresh tokens with zero reuse leeway, and 15-minute signed `at+jwt` access tokens whose audience comes from the API scopes.
- Passthrough protocol endpoints check the security stamp at refresh.
- A Razor login page with Material-like CSS.
- Startup tasks register the scopes, the two gateway clients and the platform admin.
- Signing and encryption keys per environment: ephemeral, a directory volume, or mounted certificates.
- Identity validates its own tokens locally through OpenIddict validation. That needs a small additive BuildingBlocks 0.2.0: route authorization usable with any authentication scheme, and `at+jwt` test tokens.
- Constitution v1.4.0 records the BFF and the narrow principle III exception for protocol and sign-in pages.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401), Razor Pages

**Primary Dependencies**: OpenIddict.AspNetCore and OpenIddict.EntityFrameworkCore 7.9.0; Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.15; Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.15; BuildingBlocks 0.2.0; Wolverine 6.65.1; EF Core 10 with Npgsql 10.0.4

**Storage**: the Identity Postgres database: users, OpenIddict, data protection keys and role assignments (`public`), plus the Wolverine schema. Signing and encryption certificates live in the `identity-keys` volume in dev

**Testing**: xUnit v3, Testcontainers, `WebApplicationFactory` with cookie-handling clients and no auto-redirect, test tokens with `at+jwt`, the no-roles route check

**Target Platform**: Linux containers, Docker Desktop in dev, GitHub Actions

**Project Type**: Web service (OpenID Connect provider), plus a shared package change

**Performance Goals**: the full code flow in a test in under 5s (SC-001)

**Constraints**:
- no comments
- pinned versions
- the service folder only as the build context
- tokens without roles or permissions
- no defaults for secrets

**Scale/Scope**: 4 protocol endpoints, 3 pages, about 6 new types in Identity, a small additive BuildingBlocks change, one migration

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Everything stays in the Identity folder. One database, one context |
| II. Shared code via versioned packages | ✅ | BuildingBlocks 0.2.0 from a new tag, additive |
| III. Handlers hold logic | ⚠️ justified | Protocol endpoints and sign-in pages use HttpContext sign-in APIs directly (R5). They contain no business rules. Recorded below and in the v1.4.0 amendment |
| IV. Transactions | ✅ | No handler `SaveChanges`. The UserManager and OpenIddict managers save through their own stores outside handlers |
| VI. Authorization | ✅ | Tokens carry only `sub`, `tenant_id`, `user_type`. Route categories are unchanged. New endpoints are reviewed allow-list entries. The no-roles route test stays green |
| VII. Tests isolated | ✅ | Ephemeral keys and a temp key directory in tests. Testcontainers only |
| VIII. Docker and pinning | ✅ | New volume, no new images |
| IX. No comments | ✅ | Including the `.cshtml` and `.css` files |
| C2 (SPA authentication) | ✅ after v1.4.0 | Replaced by the BFF in this feature's amendment |

**Gate: PASS**, with one justified deviation.

## Project Structure

### Documentation (this feature)

```text
specs/012-identity-authentication/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/endpoints.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/building-blocks/
├── BuildingBlocks.AspNetCore/Authentication/AuthenticationExtensions.cs   split: AddBuildingBlocksRouteAuthorization(scheme)
├── BuildingBlocks.AspNetCore.Tests/Authentication/                         test for a non-JwtBearer scheme
└── BuildingBlocks.Testing/Tokens/TestTokens.cs                            typ at+jwt
src/services/identity/
├── Directory.Packages.props                                               OpenIddict 7.9.0, Identity EF, DataProtection EF, BuildingBlocks 0.2.0
├── Dockerfile                                                             /keys owned by $APP_UID
├── compose.yaml                                                           identity-keys volume, new environment
├── Identity.Api/
│   ├── Program.cs
│   ├── Users/AppUser.cs, UserTypes.cs
│   ├── Persistence/IdentityDbContext.cs (users, Data Protection), StartupTasks.cs (replaces MigrationRunner), Migrations/
│   ├── Protocol/ProtocolSettings.cs, Scopes.cs, ClientRegistrar.cs, PlatformAdminSeeder.cs, SigningKeys.cs, PrincipalFactory.cs
│   ├── Connect/AuthorizeEndpoint.cs, TokenEndpoint.cs, EndSessionEndpoint.cs
│   ├── Pages/Account/Login.cshtml(.cs), Logout.cshtml(.cs), SignedIn.cshtml, _ViewImports.cshtml, Shared/_Layout.cshtml
│   └── wwwroot/css/identity.css
└── Identity.Tests/
    ├── IdentityAppFixture.cs (OIDC settings, ephemeral keys, test signing key, admin settings)
    ├── Flow/CodeFlow.cs (helper), CodeFlowTests.cs, TokenTests.cs, RefreshTests.cs, EndSessionTests.cs
    ├── LoginPageTests.cs, PlatformAdminSeedTests.cs, SigningKeyTests.cs
    └── existing tests, unchanged
deploy/dev.env, deploy/README.md                                           issuer, gateway origin, client secrets, admin, keys password
.specify/memory/constitution.md                                            v1.4.0
```

**Structure Decision**: Protocol code lives in `Protocol/` (configuration) and `Connect/` (endpoints), separate from the domain-like `Roles/` and `Authorization/` from 011.

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| Protocol endpoints and login pages are not Wolverine handlers (principle III) | OpenIddict passthrough and SignInManager need `HttpContext` sign-in, sign-out and challenge, which a message handler can't do | Wrapping them in a message hop adds indirection without moving any business rule. The amendment scopes the exception tightly |
| Identity entities configured by hand instead of deriving from `IdentityDbContext` | The context must derive from `TenantDbContext` for the outbox and the tenant filter | A second context splits transactions |
