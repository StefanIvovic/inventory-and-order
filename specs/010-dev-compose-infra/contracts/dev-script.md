# Contract: `scripts/dev.ps1`

```
./scripts/dev.ps1 <up|down|reset|status> [-Profile <string[]>]
```

| Command | Compose call | Effect | Exit |
|---|---|---|---|
| `up` | `--profile <each -Profile> up --detach --wait --wait-timeout 120` | Starts the chosen profiles and waits for health | 0 when healthy, non-zero on failure or timeout |
| `down` | `--profile '*' down --remove-orphans` | Removes the containers and keeps the volumes | 0 / non-zero |
| `reset` | `--profile '*' down --volumes --remove-orphans` | Removes the containers, volumes and network of the dev project only | 0 even if nothing is running |
| `status` | `--profile '*' ps --all` | Shows each container's state and health | 0 / non-zero |

- Every call uses `-f <repo>/deploy/compose.yaml`, resolved from `$PSScriptRoot`, so it works from any working directory.
- `-Profile` defaults to `infra` and is used only by `up`.
- If there is no command or the command is unknown, the script prints the usage and exits 1. Because the command is a `ValidateSet` parameter, PowerShell rejects an invalid value.
- If the Docker engine is unreachable, the script prints `Docker is not available.` and exits 2. This matches `check-test-cleanup.ps1`.
- If a compose call fails, the script exits with compose's exit code.

# Contract: `scripts/check-test-cleanup.ps1` (extended)

```
./scripts/check-test-cleanup.ps1 [-TimeoutSeconds 60] [-PollSeconds 2] [-Project <string[]>]
```

- The new `-Project` parameter defaults to empty.
- Each value adds a filter `label=com.docker.compose.project=<value>` to the containers and volumes it checks.
- With no `-Project`, the behaviour is unchanged: it checks Testcontainers and `e2e-test` only.

## Profile selection

The script selects profiles through `COMPOSE_PROFILES` (`infra` or the `-Profile` values for `up`, and `*` for the other commands), not through `--profile` arguments, and it restores the previous value afterwards. On Linux and macOS, PowerShell expands a `*` argument passed to a native command into file names, so `--profile '*'` breaks there (research.md R2).
