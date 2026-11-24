# Quickstart: Analyzers

Prerequisites: SDK 10.0.401, PowerShell 7. Docker is not needed.

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` | The analyzer tests pass, along with every earlier test |
| 2 | `dotnet build src/building-blocks/BuildingBlocks.slnx` | 0 IAO diagnostics (SC-003) |
| 3 | `pwsh scripts/pack.ps1` | The feed contains `BuildingBlocks.Analyzers`. Core has 0 dependencies. The AspNetCore, EF, Messaging and Validation nuspecs depend on Analyzers with no `Analyzers` exclude |
| 4 | `pwsh scripts/check-analyzers.ps1` | The violating consumer fails with IAO1001 and IAO1002, the clean one builds, and no temporary folder is left |

## IAO1001: what to do instead

Return `PermissionAssignmentsChanged(tenantId, userId)` from the handler that changed the role assignments. The outbox delivers it after commit to every instance.

## IAO1002: what to do instead

Remove the `SaveChanges` or `SaveChangesAsync` call. Wolverine's transaction middleware saves and commits when the handler returns. Saving belongs only in seeders, migrations, design-time factories and tests.
