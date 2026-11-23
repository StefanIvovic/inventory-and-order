# Quickstart: BuildingBlocks Core Validation

Prerequisites: SDK 10.0.401. Docker is not needed.

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` | All Core and AspNetCore tests pass |
| 2 | `pwsh scripts/pack.ps1` | The feed gets `BuildingBlocks.Core` and `BuildingBlocks.AspNetCore` with the same version, and no `*.Tests` package |
| 3 | Inspect `BuildingBlocks.Core` nuspec dependencies | No dependency groups referencing ASP.NET or other packages (SC-005) |
| 4 | Inspect `BuildingBlocks.AspNetCore` nuspec | It depends on `BuildingBlocks.Core` at the same version and has the `Microsoft.AspNetCore.App` framework reference |
| 5 | `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.AspNetCore` | The consumer restores both packages from the feed and builds |
| 6 | `dotnet run --project tools/RepoChecks -- images` | Exit 0 |
| 7 | Open the PR | The `building-blocks` workflow runs the new test step and passes |
