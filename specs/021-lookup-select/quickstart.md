# Quickstart: Lookup Select

## 1. Unit tests, lint, build

```powershell
$node = (Select-String -Path deploy/versions.env -Pattern '^NODE_IMAGE=(.+)$').Matches[0].Groups[1].Value
docker run --rm -v "${PWD}/src/web/backoffice:/app" -v /app/node_modules -w /app -e CI=true $node sh -c "npm ci && npx ng lint && npx ng test --no-watch --no-progress && npx ng build"
```

## 2. By hand (dev stack, never reset)

```powershell
./scripts/dev.ps1 up -Profile infra,apps
```

1. Sign in as `DEMO_ADMIN_EMAIL` and open Users.
2. **Status filter**: open it and see All, Active and Disabled. Type `dis` and nothing happens; press Enter and only Disabled shows. Pick it and the list shows only disabled users.
3. **Search email**: type `admin` and nothing happens; press Enter or the search button and the list filters. Clear the text and press Enter, and all users are back.
4. In the network tab, no request is sent while typing.

## 3. End-to-end

```powershell
./scripts/e2e.ps1
```

All specs pass, and nothing named `e2e-test` remains.
