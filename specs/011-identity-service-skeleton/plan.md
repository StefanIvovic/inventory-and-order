# Implementation Plan: Identity Service Skeleton

**Branch**: `011-identity-service-skeleton` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/011-identity-service-skeleton/spec.md`

## Summary

This is the first service: a runnable Identity skeleton with its own database, Wolverine messaging and code generation, the shared authentication added to BuildingBlocks, a reusable no-roles route test, and one real endpoint, `GET /roles`.

Delivery has two stages in one PR:
1. **BuildingBlocks 0.1.0.** Authentication setup, the platform-only marker, test tokens, the route check, and the `CurrentUser` platform-with-tenant rejection. It is released with the tag `building-blocks/v0.1.0`.
2. **Identity.** It pins that version, gets its packages through `scripts/restore-feed.ps1`, and builds and ships from its own folder with its own CI.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401)

**Primary Dependencies**: WolverineFx, `.Http`, `.EntityFrameworkCore`, `.Postgresql` and `.RabbitMQ` 6.65.1; `WolverineFx.RuntimeCompilation` (Debug only); EF Core 10.0.15 with Npgsql 10.0.4; `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15 (new); BuildingBlocks 0.1.0

**Storage**: Postgres 18.7 (`identity-db`). Schemas: `public` for EF and `wolverine` for messaging

**Testing**: xUnit v3, Testcontainers (Postgres and RabbitMQ, one database per test), WebApplicationFactory, test tokens signed with a test RSA key, and the no-roles route check

**Target Platform**: Linux containers (aspnet alpine), Docker Desktop for dev, GitHub Actions on ubuntu

**Project Type**: Web service, plus shared packages

**Performance Goals**: Healthy in under 90s from a clean clone (SC-001)

**Constraints**:
- build context is the service folder only
- no project references outside it
- no comments
- pinned images and packages
- Static code generation in Release
- tests isolated from dev

**Scale/Scope**: 1 endpoint, 1 table, 2 health routes, about 6 BuildingBlocks types, 1 script, 1 workflow

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Own folder, solution, Dockerfile, compose and CI. Build settings are self-contained (R9) |
| II. Shared code via versioned packages | ✅ | BuildingBlocks 0.1.0 from a tag, pinned per service. Feed restored from the tag (R1) |
| III. Handlers hold logic | ✅ | The HTTP layer maps to `GetRoles`, and `GetRolesHandler` does the work (R6) |
| IV. Transactions | ✅ | BuildingBlocks messaging: automatic transactions, outbox and inbox. No `SaveChanges` in handlers (analyzer IAO1002) |
| V. Messaging | ✅ | No events yet. Failure logging is off through BuildingBlocks |
| VI. Authorization | ✅ after v1.3.0 | `IPermissionManager` checks, fallback policy, route test, action flag. The platform category needs the amendment (R12), which is done in this feature |
| VII. Tests isolated | ✅ | Testcontainers with a database per test. The dev stack is used only by the smoke start on the CI runner |
| VIII. Docker and pinning | ✅ | Images through `versions.env` build args, loopback ports. The image-pin check covers the new Dockerfile and compose file |
| IX. No comments | ✅ | — |
| Merge gates | ✅ | Build, tests, route test, cleanup check and image-pin check all in `identity.yml` |

**Gate: PASS**, once the amendment lands in this same PR.

## Project Structure

### Documentation (this feature)

```text
specs/011-identity-service-skeleton/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/http-and-packages.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/building-blocks/
├── BuildingBlocks.Core/Identity/CurrentUser.cs                  edit: reject platform + tenant
├── BuildingBlocks.AspNetCore/Authentication/                    new: options, extensions, policies, PlatformOnlyAttribute, marker check
├── BuildingBlocks.AspNetCore.Tests/Authentication/              new tests
├── BuildingBlocks.Testing/Tokens/                               new: TestTokens, AddTestTokenValidation
├── BuildingBlocks.Testing/Routes/                               new: NoRolesRouteCheck, RouteCheckReport
├── BuildingBlocks.Testing.Tests/                                new tests
└── Directory.Packages.props                                     JwtBearer 10.0.15
src/services/identity/
├── Identity.slnx, Directory.Build.props, Directory.Packages.props, nuget.config
├── Dockerfile, compose.yaml
├── Identity.Api/
│   ├── Program.cs
│   ├── Authorization/IdentityPermissions.cs, IdentityRoles.cs, IdentityPermissionProvider.cs
│   ├── Persistence/IdentityDbContext.cs, RoleAssignment.cs, MigrationRunner.cs, Migrations/
│   ├── Roles/GetRoles.cs, GetRolesHandler.cs, RolesEndpoint.cs
│   ├── Health/ReadinessCheck.cs
│   └── Internal/Generated/                                      committed Static code
├── Identity.Contracts/Identity.Contracts.csproj                 packable, empty
└── Identity.Tests/                                              fixture, roles tests, auth tests, route test, catalogue tests
scripts/restore-feed.ps1                                         new
tools/RepoChecks/                                                new rule: service pins must not be prerelease
deploy/compose.yaml, deploy/dev.env, deploy/README.md            include, ports, credentials
.github/workflows/identity.yml                                   new
.specify/memory/constitution.md                                  v1.3.0
InventoryAndOrder.slnx                                           add the Identity projects (IDE only)
```

**Structure Decision**: This follows constitution B15. The service folder is fully self-contained so it can be the Docker build context.

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| A git tag (`building-blocks/v0.1.0`) is pushed as part of the feature | Services pin only stable versions, and MinVer derives them from tags | Prerelease pins change with every commit, and floating pins break principle II |
| The service duplicates the root build settings in its own `Directory.Build.props` | The Docker context can't see files above the service folder | Importing the root props breaks the build-context rule |
