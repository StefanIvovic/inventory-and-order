# Quickstart: API Gateway

## 1. Tests
```powershell
dotnet test --solution src/gateway/Gateway.slnx
dotnet test --solution src/services/identity/Identity.slnx
```
Expected: all green. Gateway tests need no Docker.

## 2. Dev stack
```powershell
./scripts/restore-feed.ps1 -Service identity
./scripts/dev.ps1 up -Profile infra,apps
./scripts/smoke-signin.ps1
```
Expected: the script reports backoffice sign-in, user, API call and sign-out, then storefront registration and user, all passing. `./scripts/dev.ps1 status` shows a published application port only on the gateway (`127.0.0.1:8080`).

## 3. By hand
- Open http://localhost:8080/bff/backoffice/login and sign in as `admin@demo.local`. You land on `/backoffice/`, which is 502 until the SPA features add the SPA containers.
- http://localhost:8080/bff/backoffice/user shows the user and a `logoutUrl`. The browser's cookies show only `bff-backoffice` (no `__Host-` prefix on plain HTTP), plus Identity's `identity.session`.
- http://localhost:8080/bff/shop/demo/login opens Identity's sign-in page for the Demo Shop.
- http://localhost:8080/.well-known/openid-configuration lists only `http://localhost:8080` addresses.

## 4. Clean up
```powershell
./scripts/dev.ps1 reset
```
