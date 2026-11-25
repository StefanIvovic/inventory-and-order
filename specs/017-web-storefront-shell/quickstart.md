# Quickstart: Storefront Shell

## 1. Unit tests and lint (in Docker)
```powershell
$node = (Select-String -Path deploy/versions.env -Pattern '^NODE_IMAGE=(.+)$').Matches[0].Groups[1].Value
docker run --rm -v "${PWD}/src/web/storefront:/app" -v /app/node_modules -w /app $node sh -c "npm ci && npx ng lint && npx ng test --no-watch --no-progress"
```

## 2. Dev stack
```powershell
./scripts/restore-feed.ps1 -Service identity
./scripts/dev.ps1 up -Profile infra,apps
```
- Open http://localhost:8080/shop/demo/. The frame titled "demo" shows a "Sign in" button.
- Press "Sign in", then "Create an account", and register. You land back on `/shop/demo/` with your email in the account menu.
- "Account" shows the email and "demo". "Sign out", then confirm, leaves you a visitor.
- http://localhost:8080/shop/Demo/ shows "This shop doesn't exist."

## 3. Live reload
```powershell
./scripts/dev.ps1 up -Profile infra,apps -WebDev
```
Change the home text in `src/web/storefront/src/app/home/`. It shows at `/shop/demo/` within 5 seconds.

## 4. End-to-end
```powershell
./scripts/e2e.ps1
```
Both tests pass, and nothing named `e2e-test` remains.

## 5. Clean up
```powershell
./scripts/dev.ps1 reset -WebDev
```
