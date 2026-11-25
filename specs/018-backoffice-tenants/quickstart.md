# Quickstart: Backoffice Tenants

## 1. Unit tests and lint
```powershell
$node = (Select-String -Path deploy/versions.env -Pattern '^NODE_IMAGE=(.+)$').Matches[0].Groups[1].Value
docker run --rm -v "${PWD}/src/web/backoffice:/app" -v /app/node_modules -w /app $node sh -c "npm ci && npx ng lint && npx ng test --no-watch --no-progress"
```

## 2. By hand
```powershell
./scripts/dev.ps1 up -Profile infra,apps
```
1. Sign in at http://localhost:8080/backoffice/ as `PLATFORM_ADMIN_EMAIL` with the password from `deploy/dev.env`.
2. Open "Tenants": the demo tenant is listed.
3. "Create tenant": try `demo` (shows that the identifier exists), then `acme` with `admin@acme.test` and a password of 8 or more characters. The new tenant appears first.
4. Sign out, then sign in as `admin@acme.test`: "Staff", and only "Home" in the navigation. http://localhost:8080/backoffice/tenants says "This page isn't available to you."

## 3. End-to-end
```powershell
./scripts/e2e.ps1
```
All three specs pass, and nothing named `e2e-test` remains.

## 4. Clean up
```powershell
./scripts/dev.ps1 reset
```
