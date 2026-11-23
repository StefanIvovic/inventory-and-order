# Implementation Plan: BuildingBlocks Permissions

**Branch**: `004-building-blocks-permissions` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-building-blocks-permissions/spec.md`

## Summary

This feature adds the permission abstractions, `IRequestContext` and `CommandHandler<TDbContext>`
to `BuildingBlocks.Core`. It adds to `BuildingBlocks.AspNetCore`:
- the scoped `PermissionManager`, which resolves once per request
- a singleton `PermissionCache`, with expiry controlled by `TimeProvider` (default 60 seconds)
  and immediate invalidation
- the 403 `permission-denied` mapping with `missingPermissions`
- `AddBuildingBlocksPermissions<TProvider>()`

Everything is tested in process with `FakeTimeProvider`. No Docker.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401)

**Primary Dependencies**: The ASP.NET shared framework (a private `MemoryCache`, options). Tests also use Microsoft.Extensions.TimeProvider.Testing 10.11.1

**Storage**: An in-memory cache per service instance

**Testing**: xUnit v3 4.0.3 on Microsoft.Testing.Platform, plus TestHost 10.0.15

**Target Platform**: Libraries consumed by service handlers

**Project Type**: Additions to two existing packages

**Performance Goals**: Within the cache duration, at most one provider call per (tenant, user), and at most one cache lookup per request

**Constraints**: Core stays dependency-free. No attributes or policies (constitution VI). No comments

**Scale/Scope**: About 10 types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I and II | ✅ | Additions to the existing packages. Infrastructure only, and permission names stay in services |
| III. Handlers | ✅ | `CommandHandler` base, with no logic beyond exposing context |
| IV. Transactions | ✅ | A denied check throws before any change, and feature 005's transaction rollback covers the rest |
| V. Multi-tenancy | ✅ | The cache key includes the tenant |
| VI. Decentralized authorization | ✅ | Explicit `IPermissionManager` calls. The provider is service-owned and reads the service's own database. The token carries no permissions. A denied check gives 403 with the missing permissions. No attributes or policies |
| VII. Tests isolated | ✅ | In process, with a fake clock, no Docker |
| VIII–X | ✅ / n/a | — |

**Gate: PASS**, both before and after design.

## Project Structure

### Documentation (this feature)

```text
specs/004-building-blocks-permissions/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/permissions-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code

```text
src/building-blocks/
├── Directory.Packages.props                          (adds TimeProvider.Testing 10.11.1)
├── BuildingBlocks.Core/Permissions/IPermissionManager.cs
├── BuildingBlocks.Core/Permissions/IPermissionProvider.cs
├── BuildingBlocks.Core/Permissions/IPermissionCache.cs
├── BuildingBlocks.Core/Permissions/PermissionDeniedException.cs
├── BuildingBlocks.Core/Handlers/IRequestContext.cs
├── BuildingBlocks.Core/Handlers/CommandHandler.cs
├── BuildingBlocks.AspNetCore/Permissions/PermissionOptions.cs
├── BuildingBlocks.AspNetCore/Permissions/PermissionCache.cs
├── BuildingBlocks.AspNetCore/Permissions/PermissionManager.cs
├── BuildingBlocks.AspNetCore/Handlers/RequestContext.cs
├── BuildingBlocks.AspNetCore/PermissionsExtensions.cs
├── BuildingBlocks.AspNetCore/Errors/BuildingBlocksExceptionHandler.cs   (adds the 403 mapping)
├── BuildingBlocks.Core.Tests/Permissions/PermissionDeniedExceptionTests.cs
└── BuildingBlocks.AspNetCore.Tests/
    ├── TestApp.cs                                    (adds the permission setup and endpoints)
    ├── Permissions/FakePermissionProvider.cs
    ├── Permissions/PermissionManagerTests.cs
    ├── Permissions/PermissionCacheTests.cs
    ├── Permissions/PermissionDeniedMappingTests.cs
    └── Handlers/CommandHandlerTests.cs
```

**Structure Decision**: No new projects. Everything goes into the existing packages and test projects.

## Complexity Tracking

No violations.
