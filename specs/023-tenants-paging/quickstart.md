# Quickstart: Tenants Paging

## 1. Identity tests

```powershell
dotnet test src/services/identity/Identity.slnx
./scripts/codegen.ps1 identity
```

The `ListTenants` tests cover order, paging, search, total, the cursor, validation and permission, and `TenantListPlanTests` covers cost at 20,000 tenants ([research R4](research.md#r4-plan-cost-test)). `codegen.ps1` must leave no diff after the generated code is committed.

## 2. Backoffice unit tests, lint, build

```powershell
$node = (Select-String -Path deploy/versions.env -Pattern '^NODE_IMAGE=(.+)$').Matches[0].Groups[1].Value
docker run --rm -v "${PWD}/src/web/backoffice:/app" -v /app/node_modules -w /app -e CI=true $node sh -c "npm ci && npx ng lint && npx ng test --no-watch --no-progress && npx ng build"
```

## 3. By hand (dev stack, never reset)

```powershell
./scripts/dev.ps1 up -Profile infra,apps
```

1. Sign in as the platform admin (`PLATFORM_ADMIN_EMAIL` in `deploy/dev.env`) and open Tenants.
2. The tenants are in A–Z order by identifier, with "1–N of N tenants".
3. Search `dem` and press Enter: only `demo` shows. Search part of a name in other letter case: it matches. Typing alone sends nothing.
4. Set Rows per page to 10. Create a few tenants until there are more than 10, then page with Next and Previous. In the network tab, the next page is fetched in the background.
5. Create a tenant: a confirmation shows, and the list restarts at the first page with the new total.

See [the UI contract](contracts/ui.md) and [the API contract](contracts/api.md).

## 4. End-to-end

```powershell
./scripts/e2e.ps1
```

All specs pass, and nothing named `e2e-test` remains.
