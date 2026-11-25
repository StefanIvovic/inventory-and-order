# Quickstart: Backoffice Shell

## 1. Unit tests and lint (in Docker)
```powershell
docker run --rm -v "${PWD}/src/web/backoffice:/app" -w /app <NODE_IMAGE from deploy/versions.env> sh -c "npm ci && npx ng lint && npx ng test --no-watch --no-progress"
```
Expected: lint clean, all tests green.

## 2. Normal dev stack
```powershell
./scripts/restore-feed.ps1 -Service identity
./scripts/dev.ps1 up -Profile infra,apps
```
Open http://localhost:8080/backoffice/orders. Expected:
- the sign-in screen appears
- "Sign in" goes to Identity
- after signing in as `admin@demo.local` you are back on `/backoffice/orders`, with the toolbar showing the email and "Staff"
- "Sign out" ends on the sign-in screen

## 3. Development mode with live reload
```powershell
./scripts/dev.ps1 up -Profile infra,apps -WebDev
```
Change the home text in `src/web/backoffice/src/app/home/`. Expected: http://localhost:8080/backoffice/ shows the change within 5 seconds.

## 4. End-to-end test
```powershell
./scripts/e2e.ps1
```
Expected:
- the test passes
- `docker ps -a --filter label=com.docker.compose.project=e2e-test` is empty afterwards
- the dev stack from step 2 or 3 is still running unchanged

## 5. Clean up
```powershell
./scripts/dev.ps1 reset -WebDev
```
