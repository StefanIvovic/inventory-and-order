# Quickstart: App-Wide Loading Indicator

## 1. Unit tests, lint, build

```powershell
$node = (Select-String -Path deploy/versions.env -Pattern '^NODE_IMAGE=(.+)$').Matches[0].Groups[1].Value
docker run --rm -v "${PWD}/src/web/backoffice:/app" -v /app/node_modules -w /app -e CI=true $node sh -c "npm ci && npx ng lint && npx ng test --no-watch --no-progress && npx ng build"
```

The `loading/` specs cover the delay, overlap, failures, cancellation and background marking ([research R7](research.md#r7-tests)).

## 2. By hand (dev stack, never reset)

```powershell
./scripts/dev.ps1 up -Profile infra,apps
```

1. Sign in as `DEMO_ADMIN_EMAIL`. Open DevTools and set network throttling to "Slow 4G".
2. **Users, filter**: change Role. The bar shows under the toolbar until the list arrives. There is no second bar under the filters.
3. **Background**: with more than one page of users, after the first page shows, the prefetch request runs and the bar does not show for it.
4. **Lookup select**: open Status. The panel shows its own loading row, and the global bar does not show.
5. **Dialogs**: Add staff user, then save. The bar shows until the dialog closes. Open Roles for a user and switch a role, and the bar shows while it saves.
6. **Fast server**: turn throttling off and repeat step 2. The bar does not flicker.
7. **Screen reader** (NVDA or Narrator): with throttling on, change a filter, and "Loading" is announced.

See [the contract](contracts/ui.md) for every situation.

## 3. End-to-end

```powershell
./scripts/e2e.ps1
```

All specs pass, and nothing named `e2e-test` remains.
