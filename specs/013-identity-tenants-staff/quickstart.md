# Quickstart: Identity Tenants and Staff

## 1. Tests
```powershell
./scripts/pack.ps1
dotnet test src/building-blocks/BuildingBlocks.slnx
dotnet test src/services/identity/Identity.slnx
```
Expected: all green, including the no-roles route check over the 8 tenant and platform routes.

## 2. Dev stack with the demo tenant
```powershell
./scripts/dev.ps1 up -Profile infra,apps
```
Expected: Identity is healthy. The logs show the demo tenant from `deploy/dev.env` (`DEMO_TENANT_SLUG`) created on the first start only.

## 3. By hand
1. Sign in at http://127.0.0.1:5101/account/login as `DEMO_ADMIN_EMAIL` / `DEMO_ADMIN_PASSWORD`: the "signed in" page.
2. Open the RabbitMQ management UI and check that the `Identity.Contracts.TenantCreated` and `Identity.Contracts.UserRegistered` exchanges exist.

## 4. Restart keeps the demo tenant
```powershell
./scripts/dev.ps1 down; ./scripts/dev.ps1 up -Profile infra,apps
```
Expected: no second demo tenant and no new events.

## 5. Clean up
```powershell
./scripts/dev.ps1 reset
```
