# Quickstart: Identity Service Skeleton

Prerequisites: Docker running, PowerShell 7, the .NET SDK from `global.json`.

## 1. Feed and build
```powershell
./scripts/restore-feed.ps1 -Service identity
dotnet test --solution src/services/identity/Identity.slnx
```
Expected: the BuildingBlocks 0.1.0 packages are in `artifacts/packages`, and all tests pass, including the no-roles route test (its report lists `GET /roles` as checked and the health routes as skipped).

## 2. Run in the dev stack (US1)
```powershell
./scripts/dev.ps1 up -Profile infra,apps
./scripts/dev.ps1 status
curl.exe -s -o NUL -w "%{http_code}" http://127.0.0.1:5101/health/ready
curl.exe -s -o NUL -w "%{http_code}" http://127.0.0.1:5101/roles
```
Expected: `identity-db`, `identity-api` and `rabbitmq` are healthy, readiness returns 200, and `/roles` without a token returns 401.

## 3. Readiness follows the database (US1)
```powershell
docker stop inventory-and-order-identity-db-1
curl.exe -s -o NUL -w "%{http_code}" http://127.0.0.1:5101/health/ready
curl.exe -s -o NUL -w "%{http_code}" http://127.0.0.1:5101/health/live
docker start inventory-and-order-identity-db-1
```
Expected: 503, then 200, and readiness is back to 200 shortly after the start.

## 4. Codegen guard (US5)
```powershell
dotnet run --project src/services/identity/Identity.Api -c Debug -- codegen write
git status --porcelain -- src/services/identity/Identity.Api/Internal/Generated
```
Expected: no output.

## 5. Clean up
```powershell
./scripts/dev.ps1 reset
./scripts/check-test-cleanup.ps1
```
