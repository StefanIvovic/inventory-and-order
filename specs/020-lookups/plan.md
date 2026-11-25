# Implementation Plan: Lookups

**Branch**: `020-lookups` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/020-lookups/spec.md`

## Summary

Fixed sets of choices that are stored or exposed by an API move from enums and text into each service's own `lookup_types` and `lookup_values` tables.
- **Shared machinery** (R1–R6): a new `BuildingBlocks.Lookups` package in BuildingBlocks 0.4.0 provides:
  - the entities and EF configuration
  - a seeder working from code definitions with fixed ids
  - a reader with search, ids and codes filters and keyset paging
  - an ETag middleware
- **`SignedIn` route category** (R5): open to any signed-in user, and checked for 401 by the route check.
- **Identity** (R7):
  - defines `user-type` and `staff-status`
  - serves `GET /lookups/{typeCode}/values` through a Wolverine handler (types are not exposed)
  - stores the user's type as `UserTypeId`, an FK, with a backfilling migration; the code string stays in C# through a value converter, so tokens and APIs are unchanged
- **Constitution** (R8): v1.6.0 records the rule.

## Technical Context

**Language/Version**: C# 14 on .NET 10

**Primary Dependencies**: EF Core 10 with Npgsql (existing), Wolverine HTTP (existing), FluentValidation via BuildingBlocks.Validation (existing). No new third-party packages.

**Storage**: each service's PostgreSQL 18 database. Identity gets two new tables, and `identity_users.UserType` text becomes `UserTypeId` uuid with an FK.

**Testing**:
- xunit v3
- Testcontainers PostgreSQL, one database per test (principle VII)
- BuildingBlocks tests on a small test DbContext
- Identity integration and migration tests
- the existing e2e suite as a regression check

**Target Platform**: Linux containers

**Project Type**: shared library packages and a web service

**Performance Goals**:
- a values page costs one indexed query at any depth
- a repeated request gets 304 with no body

**Constraints**:
- no comments
- provider-neutral code in BuildingBlocks
- additive changes to contracts and to `RouteCheckReport`
- the dev data survives the upgrade

**Scale/Scope**:
- 1 new package (about 12 files) and changes to 2 packages
- Identity: about 10 files changed, 1 migration, tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Each service has its own lookup tables and endpoints. No shared store |
| II. Shared code via versioned packages | ✅ | Only generic machinery goes into the `BuildingBlocks.Lookups` 0.4.0 package. The lists stay in the services. Identity pins 0.4.0. `UserRegistered` is unchanged |
| III. Handlers hold logic | ✅ | The Identity endpoints map to the `ListLookupValues` handler in `Features/Lookups/`. The reader is query plumbing, like the tenant filter |
| IV. Transactions | ✅ | Seeding runs in one transaction. The migration runs in EF's migration transaction |
| V. Multi-tenancy | ✅ | Lookups are not `ITenantOwned`, so they are global by decision 3a. Tenant data is not readable through them |
| VI. Authorization | ✅ after amendment | The new `SignedIn` category is a caller-identity marker, which principle VI already allows for categories. The no-roles test checks it for 401. v1.6.0 records it |
| VII. Tests isolated | ✅ | Testcontainers, one database per test; the e2e suite in `e2e-test` |
| VIII. Docker | ✅ | No new containers |
| IX, X | ✅ | No comments, no attribution |

**Gate: PASS.** The `SignedIn` category is added through the v1.6.0 amendment, the first commit after the docs, not as a deviation.

**Post-design re-check**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/020-lookups/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/lookups-api.md
└── tasks.md
```

### Source Code

```text
.specify/memory/constitution.md                       v1.6.0

src/building-blocks/
├── BuildingBlocks.Lookups/                           new package
│   ├── LookupType.cs, LookupValue.cs, LookupModelBuilderExtensions.cs
│   ├── LookupCatalog.cs, LookupTypeDefinition.cs, LookupValueDefinition.cs
│   ├── LookupSeeder.cs, LookupSeedException.cs
│   ├── LookupReader.cs, LookupValueQuery.cs, LookupCursor.cs, LookupPages.cs
│   └── LookupCaching.cs                              middleware and endpoint metadata
├── BuildingBlocks.Lookups.Tests/
├── BuildingBlocks.AspNetCore/Authentication/         SignedIn category, attribute, RequireSignedIn, policy
├── BuildingBlocks.Testing/Routes/                    SignedIn routes checked for 401; report gains SignedIn
├── BuildingBlocks.slnx, Directory.Packages.props     new project
└── *.Tests                                           category and route-check tests

src/services/identity/
├── Directory.Packages.props                          BuildingBlocks 0.4.0, BuildingBlocks.Lookups
├── Identity.Api/Lookups/IdentityLookups.cs           catalog
├── Identity.Api/Users/UserTypes.cs                   lookup value definitions
├── Identity.Api/Features/Lookups/                    queries, validator, handlers, endpoints
├── Identity.Api/Persistence/                         ApplyLookups, UserTypeId converter, FK, check, indexes, StartupTasks seeding
├── Identity.Api/Persistence/Migrations/              UserTypeLookup (hand-edited backfill)
├── Identity.Api/Internal/Generated/                  regenerated Wolverine code
└── Identity.Tests/Lookups/                           endpoints, seeding, migration
```

**Structure Decision**: the new package sits beside the existing BuildingBlocks packages and is released with them under the shared tag `building-blocks/v0.4.0`. Identity's lookups are a vertical slice.

## Complexity Tracking

No deviations.
