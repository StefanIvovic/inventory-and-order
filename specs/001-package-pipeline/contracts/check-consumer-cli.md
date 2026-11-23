# Contract: `scripts/check-consumer.ps1`

```text
pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.Core [-Version <v>] [-Feed artifacts/packages]
```

| Behaviour | Result |
|---|---|
| `-Version` omitted | Uses the highest version of `-PackageId` in the feed |
| Runs | Creates a temporary folder outside the repository with a consumer project that pins the version, plus a `nuget.config` with the feed and source mapping. Sets `NUGET_PACKAGES` to a temporary folder. Runs restore and build |
| The version is not in the feed | Restore fails, exit code 1, and the message names the package and version |
| Any outcome | `finally` deletes the temporary consumer and the temporary cache, and verifies that both are gone |
| Succeeds | Exit code 0 |
