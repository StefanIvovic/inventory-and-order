# Implementation Plan: Permission Invalidation

**Branch**: `007-permission-invalidation` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-permission-invalidation/spec.md`

## Summary

This feature adds to `BuildingBlocks.Messaging`:
- the `PermissionAssignmentsChanged` event
- `UseBuildingBlocksPermissionInvalidation()`, which sets up a per-service fanout exchange, one exclusive auto-deleting queue per instance, and a handler that calls `IPermissionCache.Invalidate`

Role-assignment handlers return the event, so the outbox delivers it only after commit. The
constitution is amended to v1.2.0 with the invalidation rule, the renamed handler types and the
code-generation decision. Everything is tested with two instances of one service plus a second
service, on real containers.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401)

**Primary Dependencies**: The existing BuildingBlocks.Messaging and Wolverine 6.65.1. No new packages

**Storage**: Each service's PostgreSQL database. Test-only `PermissionGrant` rows

**Testing**: xUnit v3, BuildingBlocks.Testing fixtures, and several hosts per test

**Target Platform**: ASP.NET Core services

**Project Type**: An addition to an existing package, plus a constitution amendment

**Performance Goals**: One small broadcast message per changed user

**Constraints**: Invalidate only after commit. No per-instance queues may be left behind. No comments

**Scale/Scope**: 3 types and 1 amendment

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Per-service exchange, so no cross-service coupling |
| II. Shared code | ✅ | Infrastructure event, in BuildingBlocks |
| IV. Transactions | ✅ | Emitted through the outbox, after commit |
| VI. Authorization | ✅ | Permission changes take effect on every instance. The amendment adds the invalidation rule |
| VII. Tests isolated | ✅ | Real containers, nothing left behind, queues auto-delete |
| Others | ✅ / n/a | — |

**Gate: PASS**, both before and after design. The amendment is part of this feature (FR-006).

## Project Structure

```text
.specify/memory/constitution.md                                   (v1.2.0)
src/building-blocks/BuildingBlocks.Messaging/
├── PermissionAssignmentsChanged.cs
├── PermissionAssignmentsChangedHandler.cs
└── PermissionInvalidationExtensions.cs
src/building-blocks/BuildingBlocks.Messaging.Tests/
├── TestModel.cs                                                  (adds PermissionGrant, GrantPermission, and a DB-backed provider)
├── TestServiceHost.cs                                            (adds the /permissions endpoint and durability and invalidation options)
└── PermissionInvalidationTests.cs
```

## Complexity Tracking

No violations.
