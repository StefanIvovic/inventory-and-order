# Quickstart: Development Compose Infrastructure

Prerequisites: Docker Desktop is running, and PowerShell 7 is installed.

## 1. Start and use the broker (US1)
```powershell
./scripts/dev.ps1 up
./scripts/dev.ps1 status
```
- Expected: `rabbitmq` shows `running (healthy)` under the project `inventory-and-order`.
- Open http://127.0.0.1:15672 and sign in with `dev` / `dev-only-not-a-secret`.
- Run `docker port inventory-and-order-rabbitmq-1`. Expected: only `127.0.0.1` bindings.

## 2. Persistence (US1)
```powershell
./scripts/dev.ps1 down
./scripts/dev.ps1 up
```
- Expected: a queue created earlier in the UI is still there.

## 3. Reset is scoped (US2)
```powershell
docker run -d --name canary --label com.docker.compose.project=canary ${env:RABBITMQ_IMAGE} sleep 3600
./scripts/dev.ps1 reset
docker ps --filter name=canary
docker volume ls --filter label=com.docker.compose.project=inventory-and-order
docker rm -f canary
```
- Before the first command, set `$env:RABBITMQ_IMAGE` from `deploy/versions.env`.
- Expected: the canary is still running, and no dev volumes are listed.

## 4. Plain compose still works (edge case)
```powershell
docker compose -f deploy/compose.yaml --profile infra config --quiet
```
- Expected: exit code 0, with no warnings about unset variables.

## 5. Pin and cleanup checks
```powershell
dotnet run --project tools/RepoChecks -- images
./scripts/check-test-cleanup.ps1
```
- Expected: both pass. With the dev stack running, the default cleanup check still passes.

## 6. CI (US4)
- Push a change under `deploy/`.
- Expected: `deploy.yml` runs, passes, and its last step reports no leftovers.
