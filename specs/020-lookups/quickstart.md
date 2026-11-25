# Quickstart: Lookups

## 1. BuildingBlocks

```powershell
dotnet test src/building-blocks/BuildingBlocks.slnx
./scripts/pack.ps1
./scripts/check-consumer.ps1
./scripts/check-analyzers.ps1
./scripts/check-test-cleanup.ps1
```

All tests pass, including:
- the `BuildingBlocks.Lookups` seeder, reader and caching tests
- the `SignedIn` category tests
- the route-check tests

## 2. Identity

```powershell
./scripts/restore-feed.ps1 -Service identity
dotnet test src/services/identity/Identity.slnx
./scripts/codegen.ps1 -Service identity -Check
```

All tests pass, including:
- the lookup values endpoint for staff, customer, platform and anonymous callers
- filters and 304
- the seeder on an empty, a changed and a repeated start
- the `UserTypeLookup` migration test
- the no-roles route check, whose report now lists the lookup route as `SignedIn`

## 3. Dev stack (no reset)

```powershell
./scripts/dev.ps1 up -Profile infra,apps
```

1. Sign in to the backoffice as the platform admin, then open http://localhost:8080/api/identity/lookups/user-type/values in the same browser. Three values come back.
2. Reload the page. The browser's network tab shows 304.
3. The demo admin and the customers created earlier still sign in. Their users kept their type.

## 4. End-to-end

```powershell
./scripts/e2e.ps1
```

The existing specs pass, and nothing named `e2e-test` remains.
