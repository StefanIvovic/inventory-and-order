# Quickstart: EF Tenancy Validation

Prerequisites: SDK 10.0.401. Docker is not needed.

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` | All tests pass, including the new EF tenancy tests |
| 2 | `pwsh scripts/pack.ps1` | The feed contains `BuildingBlocks.Core`, `BuildingBlocks.AspNetCore` and `BuildingBlocks.EntityFrameworkCore` at the same version, and no Tests packages |
| 3 | Inspect the `BuildingBlocks.EntityFrameworkCore` nuspec | It depends on `BuildingBlocks.Core` and `Microsoft.EntityFrameworkCore` 10.0.15, with no provider package |
| 4 | `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.EntityFrameworkCore` | It restores and builds from the feed |
| 5 | `dotnet run --project tools/RepoChecks -- images` | Exit 0 |

Service notes:
- A design-time factory for migrations constructs the context with `new TenantContext()`.
- Bulk operations (`ExecuteUpdate`, `ExecuteDelete`) respect the tenant filter when selecting rows, but they bypass the SaveChanges interceptor. They MUST never set `TenantId`.
