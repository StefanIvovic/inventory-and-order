# Quickstart: Identity Authentication

Prerequisites: Docker running, PowerShell 7, the .NET SDK from `global.json`.

## 1. Tests
```powershell
./scripts/restore-feed.ps1 -Service identity
dotnet test --solution src/services/identity/Identity.slnx
./scripts/codegen.ps1 -Service identity -Check
```
Expected: all tests pass, including the full code-flow tests, lockout, seeding and the no-roles route test, and the generated code is up to date.

## 2. Dev stack
```powershell
./scripts/dev.ps1 up -Profile infra,apps
curl.exe -s http://127.0.0.1:5101/.well-known/openid-configuration
```
Expected: the discovery document with `issuer` = `http://localhost:8080/`, the authorization, token and end-session endpoints, and `S256` among the code challenge methods.

## 3. Login page by hand
Open http://127.0.0.1:5101/account/login and sign in with `PLATFORM_ADMIN_EMAIL` / `PLATFORM_ADMIN_PASSWORD` from `deploy/dev.env`. Expected: the "signed in" page. Enter a wrong password 5 times: expected the lockout message.

Timing (SC-001): run `dotnet test src/services/identity/Identity.Tests --filter FullFlowIssuesTokensAndTheyWorkOnTheApi --logger "console;verbosity=normal"` on a warm machine. Expected: the test finishes in under 5 seconds.

## 4. Keys survive a restart (US6)
```powershell
curl.exe -s http://127.0.0.1:5101/.well-known/jwks > jwks-before.json
docker restart inventory-and-order-identity-api-1
curl.exe -s http://127.0.0.1:5101/.well-known/jwks > jwks-after.json
Compare-Object (Get-Content jwks-before.json) (Get-Content jwks-after.json)
```
Expected: no differences. Delete both files afterwards.

## 5. Clean up
```powershell
./scripts/dev.ps1 reset
./scripts/check-test-cleanup.ps1
```
