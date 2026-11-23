# Implementation Plan: BuildingBlocks Core

**Branch**: `002-building-blocks-core` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-building-blocks-core/spec.md`

## Summary

This feature fills `BuildingBlocks.Core` with:
- the current-user abstraction
- the claim names it reads
- the set-once tenant context
- the domain error types

It also adds `BuildingBlocks.AspNetCore`, which provides:
- identity validation (401 for malformed identity)
- tenant population per request
- one exception handler that maps errors to RFC 9457 problem details (409, 422, 404, 500) with `code` and `traceId`

A service enables all of it with `AddBuildingBlocks()` and `UseBuildingBlocks()`. Both packages ship through the 001 pipeline. They are tested with unit tests and in-process TestServer tests, with no Docker.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401)

**Primary Dependencies**: Core uses only the BCL. AspNetCore uses `Microsoft.AspNetCore.App` (framework reference)

**Storage**: N/A

**Testing**: xUnit v3 4.0.3 on Microsoft.Testing.Platform, plus Microsoft.AspNetCore.TestHost 10.0.15

**Target Platform**: Libraries consumed by ASP.NET Core services

**Project Type**: Two NuGet library packages

**Performance Goals**: Claims are parsed once per request. The middleware adds no allocations beyond one `CurrentUser` record

**Constraints**: No web dependency in Core (SC-005). No comments. No authentication setup in this feature (FR-012)

**Scale/Scope**: About 12 public types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Packages only. Services will consume them by pinned version |
| II. Shared code via packages, infrastructure only | ✅ | Identity, tenancy and error plumbing only. No domain types. `DomainException` is a generic error carrier, not domain logic |
| III. Handlers and domain | ✅ | Gives the domain `DomainException` and maps it centrally, so handlers translate nothing |
| IV. Transactions and messaging | n/a | Feature 005 |
| V. Multi-tenancy | ✅ | The tenant context is resolved from `tenant_id`. Cross-tenant access → 404 is supported by `NotFoundException`. The EF filter comes in feature 003 |
| VI. Authorization | ✅ | The token carries only `sub`, `tenant_id` and `user_type`, and only those are read. Permissions come in feature 004 |
| VII. Tests isolated | ✅ | In-process TestServer, no Docker, no ports, no shared state |
| VIII. Docker and pinned images | n/a | No images |
| IX and X | ✅ | No comments, no attribution |
| Workflow | ✅ | One feature, one PR |

**Gate: PASS**, both before and after design.

## Project Structure

### Documentation (this feature)

```text
specs/002-building-blocks-core/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── core-api.md
│   ├── aspnetcore-api.md
│   └── problem-details.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code

```text
src/building-blocks/
├── Directory.Build.props                  (IsPackable excludes *.Tests)
├── Directory.Packages.props               (adds TestHost and xunit.v3)
├── BuildingBlocks.slnx                    (adds the new projects)
├── BuildingBlocks.Core/
│   ├── Identity/AppClaimTypes.cs
│   ├── Identity/UserType.cs
│   ├── Identity/ICurrentUser.cs
│   ├── Identity/CurrentUser.cs
│   ├── Tenancy/ITenantContext.cs
│   ├── Tenancy/TenantContext.cs
│   ├── Errors/DomainErrorKind.cs
│   ├── Errors/DomainException.cs
│   └── Errors/NotFoundException.cs
├── BuildingBlocks.AspNetCore/
│   ├── BuildingBlocks.AspNetCore.csproj
│   ├── Identity/HttpCurrentUser.cs
│   ├── Identity/IdentityMiddleware.cs
│   ├── Errors/BuildingBlocksExceptionHandler.cs
│   ├── Errors/ProblemDetailsSetup.cs
│   ├── ExceptionHandlerStartupFilter.cs
│   └── BuildingBlocksExtensions.cs
├── BuildingBlocks.Core.Tests/
└── BuildingBlocks.AspNetCore.Tests/
    └── TestAuthHandler.cs
.github/workflows/building-blocks.yml      (adds the building-blocks test step)
InventoryAndOrder.slnx                     (adds the new projects)
```

**Structure Decision**: The projects live inside `src/building-blocks/`, next to the existing Core project, with tests beside them. That folder is a self-contained unit with its own solution and CPM file.

## Complexity Tracking

No violations.
