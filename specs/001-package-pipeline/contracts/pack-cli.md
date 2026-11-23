# Contract: `scripts/pack.ps1`

```text
pwsh scripts/pack.ps1 [-Configuration Release] [-Output artifacts/packages]
```

| Behaviour | Result |
|---|---|
| Discovers every project with `IsPackable=true` under `src/` | Packs each one into `-Output` (created if missing) |
| Produces a prerelease version | Clears `<globalPackages>/<id-lower>/<version>` so consumers see the new content |
| Produces a release version that already exists in the feed with different content | Fails, exit code 1, and names the package |
| Finds a packable project with no tag prefix | The build error from `Directory.Build.targets` → exit code 1 |
| Succeeds | Prints one `id version` line per package, exit code 0 |
