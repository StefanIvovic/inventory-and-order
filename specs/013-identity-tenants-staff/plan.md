# Implementation Plan: Identity Tenants and Staff

**Branch**: `013-identity-tenants-staff` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/013-identity-tenants-staff/spec.md`

## Summary

Identity gains tenants, staff management and its own roles. The plan:
- A `Tenant` entity and platform-only routes `POST /tenants` and `GET /tenants`, guarded by a new platform role (Platform Administrator) stored in a separate, non-tenant assignment table (R2).
- Staff routes `POST /users`, `GET /users`, `POST /users/{id}/disable`, and role routes `PUT` / `DELETE /users/{id}/roles/{role}`, each a Wolverine handler on `CommandHandler<IdentityDbContext>`.
- `AppUser` gets a disabled state. Login, authorize and refresh refuse disabled users (R6).
- Events `TenantCreated`, `UserRegistered`, `UserDisabled` in the existing `Identity.Contracts` project, released as `identity-contracts/v0.1.0`, published through the EF outbox. Role changes and disabling return `PermissionAssignmentsChanged` (R7).
- Request validation after the permission check. Wolverine runs FluentValidation before any handler code (verified by a spike, R3), so BuildingBlocks 0.3.0 adds explicit command validation: `CommandValidator<T>` plus `ICommandValidation.EnsureValidAsync`, called by the handler after `Permissions.EnsureAsync`.
- A partial unique index for non-customer emails, unique slugs, and a 409 mapper for unique violations so races return 409 (R4).
- Tenant creation shared by the handler and the dev demo seed through a domain service (R8).

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401)

**Primary Dependencies**: BuildingBlocks 0.3.0 (AspNetCore, EntityFrameworkCore, Messaging, Validation, Testing); Wolverine 6.65.1 with WolverineFx.FluentValidation via BuildingBlocks.Validation; ASP.NET Core Identity 10.0.15; OpenIddict 7.9.0; EF Core 10 with Npgsql 10.0.4

**Storage**: the Identity Postgres database: new `identity_tenants` and `identity_platform_role_assignments` tables, new columns on `identity_users`, a changed email index; the Wolverine outbox in the same database

**Testing**: xUnit v3, Testcontainers (Postgres, RabbitMQ), `WebApplicationFactory`, `TestTokens`, Wolverine tracked sessions for events, the no-roles route check

**Target Platform**: Linux containers, GitHub Actions

**Project Type**: Web service, plus a shared package change and a contracts package

**Performance Goals**: none beyond the defaults; lists are small in this phase

**Constraints**:
- no comments
- pinned versions, tagged packages only
- no `SaveChanges` in handlers (IAO1002), no `IPermissionCache` outside BuildingBlocks (IAO1001)
- permission check before validation on every command

**Scale/Scope**: 7 new routes (8 with `GET /roles`), about 20 new types in Identity, 3 event records, a small additive BuildingBlocks change, one migration

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Everything stays in the Identity folder. Other services only reference the contracts package |
| II. Shared code via versioned packages | ✅ | BuildingBlocks 0.3.0 (additive) and `Identity.Contracts` 0.1.0 from tags |
| III. Handlers hold logic | ✅ | Every new route maps to a handler on `CommandHandler<IdentityDbContext>`. Tenant creation rules live in a domain service shared with the seed |
| IV. Transactions and messaging | ✅ | One transaction per handler. `UserManager` runs with `AutoSaveChanges = false` inside handlers so Wolverine saves (R5). Events through the outbox |
| VI. Authorization | ✅ | Explicit `Permissions.EnsureAsync` first in every handler. Platform routes check a platform permission. Cache invalidation only by returning `PermissionAssignmentsChanged`. The no-roles route test covers all 7 routes |
| VII. Tests isolated | ✅ | Testcontainers only |
| VIII. Docker and pinning | ✅ | No new images |
| IX. No comments | ✅ | |

**Gate: PASS**, no deviations.

**Post-design re-check**: PASS. The explicit validation in R3 keeps validators in the feature folder next to the command, as principle III requires.

## Project Structure

### Documentation (this feature)

```text
specs/013-identity-tenants-staff/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── endpoints.md
│   └── events.md
└── tasks.md
```

### Source Code (repository root)

```text
src/building-blocks/
├── BuildingBlocks.Validation/        CommandValidator<T>, ICommandValidation, registration split (R3)
└── BuildingBlocks.Validation.Tests/

src/services/identity/
├── Directory.Packages.props          BuildingBlocks 0.3.0, BuildingBlocks.Validation
├── Identity.Contracts/               TenantCreated, UserRegistered, UserDisabled
├── Identity.Api/
│   ├── Authorization/                new permissions, tenant and platform roles, provider reads both tables
│   ├── Tenants/                      Tenant entity, TenantCreation domain service, DemoTenantSeeder
│   ├── Features/CreateTenant/        command, validator, handler, endpoint
│   ├── Features/ListTenants/
│   ├── Features/CreateStaffUser/
│   ├── Features/ListUsers/
│   ├── Features/DisableUser/
│   ├── Features/AssignRole/          assign and unassign
│   ├── Persistence/                  model, migration, PlatformRoleAssignment, UniqueViolationMapper
│   ├── Users/AppUser.cs              disabled state
│   ├── Connect/, Pages/Account/      disabled checks
│   └── Program.cs                    validation, event routing, store AutoSaveChanges
└── Identity.Tests/                   per-story tests, events, route check
```

**Structure Decision**: Vertical slices under `Features/` as the constitution requires. The 011 `Roles/` slice moves to `Features/ListRoles/` for consistency.

## Complexity Tracking

No constitution violations to justify.
