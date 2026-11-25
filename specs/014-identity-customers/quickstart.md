# Quickstart: Identity Customers

## 1. Tests
```powershell
dotnet test src/services/identity/Identity.slnx
```
Expected: all green, including the storefront flow, client policies and the route check.

## 2. By hand on the dev stack
```powershell
./scripts/restore-feed.ps1 -Service identity
./scripts/dev.ps1 up -Profile infra,apps
```
Open http://127.0.0.1:5101/account/register?tenant=demo, register `shopper@mail.test`. Expected: the "signed in" page. Open http://127.0.0.1:5101/account/register?tenant=nope. Expected: "This shop does not exist."

## 3. Clean up
```powershell
./scripts/dev.ps1 reset
```
