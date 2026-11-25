# Implementation Plan: Identity Customers

**Branch**: `014-identity-customers` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/014-identity-customers/spec.md`

## Summary

Customers register and sign in per tenant through the storefront client. The plan:
- A `ClientPolicies` table maps each gateway client to the user types it accepts and whether it needs a tenant (R1). `AuthorizeAsync` reads the custom `tenant` parameter, resolves the slug, rejects bad storefront requests, and treats a mismatching login session as no session.
- User names carry the account's identity: staff and platform keep the email, customers get `<tenantId>:<email>` (R2). The login page derives the user name from the authorization request in its return URL, so `PasswordSignInAsync` finds the right account and never a wrong-type one. `RequireUniqueEmail` is turned off; uniqueness comes from the user-name index plus the two partial email indexes.
- Registration is a Razor page that sends a `RegisterCustomer` command to a Wolverine handler (R3). The handler creates the customer and returns `UserRegistered` through the outbox; the page then signs the customer in.
- A migration adds `ux_identity_users_customer_email` on tenant plus normalized email for customers (R4).
- Staff management filters to staff (R5).

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401), Razor Pages

**Primary Dependencies**: unchanged from 013: BuildingBlocks 0.3.0, Identity.Contracts 0.1.0 (project reference), OpenIddict 7.9.0, ASP.NET Core Identity 10.0.15, Wolverine 6.65.1, EF Core 10 with Npgsql 10.0.4

**Storage**: the Identity database: one new partial unique index; no new tables

**Testing**: xUnit v3, Testcontainers, `WebApplicationFactory` acting as both gateway clients, tracked sessions, the no-roles route check

**Target Platform**: Linux containers, GitHub Actions

**Project Type**: Web service

**Performance Goals**: none beyond the defaults

**Constraints**: no comments; no `SaveChanges` in handlers; sign-in never reveals other accounts; no package version changes

**Scale/Scope**: 1 page, 1 command handler, changes to the authorize endpoint and login page, 1 migration

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Identity folder only |
| II. Shared code via versioned packages | ✅ | No package changes. `UserRegistered` is unchanged |
| III. Handlers hold logic | ⚠️ justified | Registration logic is in the `RegisterCustomer` handler. The page only collects input, sends the command and signs in, the sign-in part being the existing exception for sign-in pages. The handler does not derive from `CommandHandler<T>`, because the caller is anonymous and has no user or permissions (R3) |
| IV. Transactions and messaging | ✅ | One transaction, `UserRegistered` through the outbox, `HandlerUserManager` without autosave |
| VI. Authorization | ✅ | Registration is a reviewed allow-list page, like login. Customers get no Identity permissions. The route test covers the API unchanged |
| VII. Tests isolated | ✅ | Testcontainers only |
| IX. No comments | ✅ | Including `.cshtml` |

**Gate: PASS**, with one justified deviation.

**Post-design re-check**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/014-identity-customers/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── protocol.md
└── tasks.md
```

### Source Code

```text
src/services/identity/
├── Identity.Api/
│   ├── Protocol/ClientPolicies.cs           client → accepted user types, tenant required
│   ├── Protocol/SignInTarget.cs             tenant and client from an authorize return URL
│   ├── Connect/ConnectEndpoints.cs          tenant parameter, session match
│   ├── Features/RegisterCustomer/           command, validator, handler
│   ├── Pages/Account/Login.cshtml(.cs)      tenant-aware user name, register link
│   ├── Pages/Account/Register.cshtml(.cs)   new
│   ├── Users/AppUser.cs                     customer user name
│   ├── Users/TenantUsers.cs                 staff only
│   ├── Features/ListUsers/                  staff only
│   ├── Persistence/                         index, migration, mapper entry
│   └── Program.cs                           RequireUniqueEmail off
└── Identity.Tests/                          storefront flow, registration, client policies, staff separation
```

**Structure Decision**: the existing vertical-slice layout; the registration page sits with the login page under `Pages/Account`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Anonymous handler not on `CommandHandler<T>` | Registration has no signed-in user or permissions | The base class needs an authenticated request context |
