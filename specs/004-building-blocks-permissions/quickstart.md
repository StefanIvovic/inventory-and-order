# Quickstart: Permissions Validation

Prerequisites: SDK 10.0.401. Docker is not needed.

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` | All tests pass, including the permission, cache, 403-mapping and command-handler tests |
| 2 | `pwsh scripts/pack.ps1` | Three packages at one version. Core's nuspec still has no dependencies |
| 3 | `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.AspNetCore` | It builds |
| 4 | `dotnet run --project tools/RepoChecks -- images` | Exit 0 |
