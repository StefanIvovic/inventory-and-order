# Quickstart: Backoffice Staff

## 1. Unit tests and lint
```powershell
$node = (Select-String -Path deploy/versions.env -Pattern '^NODE_IMAGE=(.+)$').Matches[0].Groups[1].Value
docker run --rm -v "${PWD}/src/web/backoffice:/app" -v /app/node_modules -w /app $node sh -c "npm ci && npx ng lint && npx ng test --no-watch --no-progress"
```

## 2. By hand (the dev stack stays up)
```powershell
./scripts/dev.ps1 up -Profile infra,apps
```
1. Sign in at http://localhost:8080/backoffice/ as `DEMO_ADMIN_EMAIL`. Open Users: you are listed as Administrator.
2. Open Roles: the permissions of each role are listed.
3. "Add staff user" `clerk@demo.test`. Open its "Roles" and switch on Viewer.
4. Sign in as the clerk: Users without any management button.
5. As the admin, "Disable" the clerk. Signing in as the clerk now fails.

## 3. End-to-end
```powershell
./scripts/e2e.ps1
```
All four specs pass, and nothing named `e2e-test` remains.
