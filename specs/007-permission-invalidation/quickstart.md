# Quickstart: Permission Invalidation Validation

Prerequisites: SDK 10.0.401, PowerShell 7, and Docker running.

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` | All tests pass, including the multi-instance invalidation tests |
| 2 | `pwsh scripts/check-test-cleanup.ps1` | Exit 0 |
| 3 | `pwsh scripts/pack.ps1` and `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.Messaging` | It builds |
| 4 | Read `.specify/memory/constitution.md` | Version 1.2.0. It contains the invalidation rule, `MessageHandler<TDbContext>` / `IEventContext`, and the code-generation decision |

## Rule for services

Return `PermissionAssignmentsChanged` from the handler that changed the role assignments. Never inject `IPermissionCache` into a handler.
