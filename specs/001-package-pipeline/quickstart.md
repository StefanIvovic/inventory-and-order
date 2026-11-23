# Quickstart: Package Pipeline Validation

Prerequisites: .NET SDK 10.0.401 and PowerShell 7. Docker is not needed.

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet --version` in the repo root | `10.0.401`. Any other SDK fails here because of `global.json` |
| 2 | `dotnet test tools/RepoChecks.slnx` | All tests pass, and no temporary fixture folders remain |
| 3 | Add a scratch project with no settings and a nullable warning, then build it | It inherits the baseline and the build fails on the warning. Delete the scratch project afterwards |
| 4 | `dotnet run --project tools/RepoChecks -- images` | Exit 0, no findings |
| 5 | `Measure-Command { pwsh scripts/pack.ps1 }` | `artifacts/packages/BuildingBlocks.Core.0.0.0-alpha.0.<n>.nupkg` is produced in under 2 minutes (SC-001) |
| 6 | `git tag building-blocks/v0.1.0` on a scratch branch, then step 5 again | The package version is `0.1.0`. Delete the tag afterwards |
| 7 | `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.Core` | Exit 0, and no temporary consumer folder remains |
| 8 | `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.Core -Version 9.9.9` | Exit 1 with "not found", and still no temporary folder |
| 9 | Remove `MinVerTagPrefix` from `src/building-blocks/Directory.Build.props`, then step 5 | Fails with an error naming the project. Revert afterwards |
| 10 | Open the PR | The `building-blocks` workflow runs and passes |
