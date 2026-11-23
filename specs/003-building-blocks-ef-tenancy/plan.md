# Implementation Plan: BuildingBlocks EF Tenancy

**Branch**: `003-building-blocks-ef-tenancy` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-building-blocks-ef-tenancy/spec.md`

## Summary

This feature adds `ITenantOwned` to `BuildingBlocks.Core` and creates the
`BuildingBlocks.EntityFrameworkCore` package. Its `TenantDbContext` base class:
- applies a named `"Tenant"` query filter to every tenant-owned entity (fail closed when there
  is no tenant)
- configures the tenant column (required, max length 64, indexed)
- adds a SaveChanges interceptor that stamps new rows and rejects wrong, missing or changed
  tenants before anything is written

`AcrossAllTenants()` is the explicit per-query bypass. Everything is tested against SQLite in
memory, with no Docker.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401)

**Primary Dependencies**: Microsoft.EntityFrameworkCore 10.0.15. The tests also use Microsoft.EntityFrameworkCore.Sqlite 10.0.15

**Storage**: Provider-agnostic. SQLite in memory for tests, PostgreSQL in services later

**Testing**: xUnit v3 4.0.3 on Microsoft.Testing.Platform

**Target Platform**: Libraries consumed by service data layers

**Project Type**: NuGet library package

**Performance Goals**: The filter is a parameterized equality on an indexed column. The interceptor makes one pass over tracked tenant-owned entries per save

**Constraints**: Core stays dependency-free. No provider dependency in the package. No comments

**Scale/Scope**: About 6 public types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | A package consumed by pinned version |
| II. Shared code, infrastructure only | ✅ | Tenancy plumbing, no domain types |
| III. Handlers and domain | ✅ | Domain entities implement a Core marker only |
| IV. Transactions | ✅ | The interceptor throws before any command is sent, so a failed save writes nothing. This is compatible with Wolverine's transaction middleware in feature 005 |
| V. Multi-tenancy | ✅ | Implements the global query filter and the SaveChanges interceptor exactly as the constitution names them |
| VI. Authorization | n/a | — |
| VII. Tests isolated | ✅ | SQLite in memory, no Docker, no ports, no files |
| VIII. Docker and pinned images | n/a | — |
| IX and X | ✅ | — |

**Gate: PASS**, both before and after design.

## Project Structure

### Documentation (this feature)

```text
specs/003-building-blocks-ef-tenancy/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/ef-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code

```text
src/building-blocks/
├── Directory.Packages.props                       (adds EF Core and Sqlite 10.0.15)
├── BuildingBlocks.slnx                            (adds 2 projects)
├── BuildingBlocks.Core/Tenancy/ITenantOwned.cs
├── BuildingBlocks.EntityFrameworkCore/
│   ├── BuildingBlocks.EntityFrameworkCore.csproj
│   ├── TenantFilter.cs
│   ├── TenantDbContext.cs
│   ├── TenantSaveChangesInterceptor.cs
│   ├── TenantViolationException.cs
│   └── TenantQueryableExtensions.cs
└── BuildingBlocks.EntityFrameworkCore.Tests/
    ├── TestModel.cs
    ├── SqliteFixture.cs
    ├── QueryFilterTests.cs
    ├── WriteGuardTests.cs
    ├── BypassTests.cs
    └── AdoptionTests.cs
InventoryAndOrder.slnx                             (adds 2 projects)
```

**Structure Decision**: Same pattern as 002: the package and its tests live next to each other
in `src/building-blocks/`.

## Complexity Tracking

No violations.
