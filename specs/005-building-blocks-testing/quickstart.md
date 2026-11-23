# Quickstart: Testing Package Validation

Prerequisites: SDK 10.0.401, PowerShell 7, and Docker running.

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` | All tests pass, including the container tests |
| 2 | `pwsh scripts/check-test-cleanup.ps1` | Exit 0, with no leftovers |
| 3 | `docker run -d --label org.testcontainers=true --name iao-probe <POSTGRES_IMAGE> sleep 300`, then step 2 with `-TimeoutSeconds 5` | Exit 1, listing `iao-probe`. Then `docker rm -f iao-probe` |
| 4 | `dotnet run --project tools/RepoChecks -- images` | Exit 0, and `RYUK_IMAGE` passes |
| 5 | `pwsh scripts/pack.ps1` | The feed contains `BuildingBlocks.Testing` at the same version as the other packages |
| 6 | `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.Testing` | It builds |
| 7 | Start any container on port 5432 or 5672, then run step 1 | It still passes, with no port conflict (SC-004). Stop the container afterwards |
