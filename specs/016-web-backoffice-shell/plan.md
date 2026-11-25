# Implementation Plan: Backoffice Shell

**Branch**: `016-web-backoffice-shell` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/016-web-backoffice-shell/spec.md`

## Summary

An Angular 22 backoffice app in `src/web/backoffice/` (zoneless, standalone, Angular Material) is served by nginx-unprivileged under `/backoffice/` behind the gateway.
- A small session module asks the gateway who is signed in and renders one of four states: loading, sign-in, unavailable, or the frame. It sends the user to the gateway's login and logout addresses.
- An interceptor adds `X-Bff-App` to `/api/**` calls and handles 401 and 503 (research.md R4).
- Development runs in Docker: an extra compose file swaps the gateway's backoffice upstream to an `ng serve` container with live reload (R5).
- The first Playwright test runs against a throwaway `e2e-test` stack. The browser shares the gateway's network namespace, so it sees `localhost:8080` and nothing is published (R7).

## Technical Context

**Language/Version**: TypeScript 6.0 with Angular 22.3; nginx configuration; PowerShell for scripts

**Primary Dependencies**: Angular 22.3 (core, router, Material, CDK), angular-eslint 22.8.0, Vitest with jsdom (from `ng new`), `@playwright/test` 1.65.1

**Storage**: none. Nothing is kept in the browser; the session lives in the gateway cookie.

**Testing**: Vitest unit tests through `ng test`; Playwright end-to-end tests in the `e2e-test` compose project

**Target Platform**: modern evergreen browsers; Linux containers (`node:26.13.1-alpine3.24`, `nginxinc/nginx-unprivileged:1.30.6-alpine3.24`, `mcr.microsoft.com/playwright:v1.65.1-noble`)

**Project Type**: web frontend (SPA) plus test harness

**Performance Goals**: live reload under 5 seconds (SC-004); no other targets

**Constraints**:
- no comments in any file, including generated ones
- no OIDC library
- no external CDNs or fonts (the CSP allows only `self`)
- no shared code with the storefront
- tests never touch the dev stack

**Scale/Scope**: about 10 source files in the app, 1 nginx config, 1 Dockerfile, 2 compose files, 1 end-to-end spec, 2 workflows, script changes, a one-line gateway change

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Own folder, own build context, own workflow on `src/web/backoffice/**`. The end-to-end workflow is a cross-service check like `deploy.yml`, not a service workflow |
| II. Shared code via packages | ✅ | Nothing shared. The session module is copied into the storefront in 017 (owner decision) |
| III. Handlers hold logic | ✅ n/a | No backend code |
| IV. Transactions and messaging | ✅ n/a | |
| V. Multi-tenancy | ✅ | The tenant comes only from the session; the app shows no tenant data yet |
| VI. Authorization | ✅ | The app decides nothing. It shows what the session says, and later screens use the API's action flags. Its only gateway routes are the existing reviewed SPA routes |
| VII. Tests isolated | ✅ | End-to-end runs in `-p e2e-test`, with `tmpfs` instead of named volumes, no published ports, `down -v --remove-orphans` in `finally`, and the cleanup check. Unit tests need no infrastructure |
| VIII. Docker | ✅ | Node, nginx and Playwright images are pinned in `versions.env` and passed as build arguments with no defaults. The dev server runs in Docker too |
| IX. No comments | ✅ | Generated CLI comments are removed (R2), and the nginx config has none |
| X. No attribution | ✅ | |

**Gate: PASS**, with no deviations.

**Post-design re-check**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/016-web-backoffice-shell/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── ui.md            screen states, texts, navigation targets
│   └── serving.md       paths, cache headers, security headers, health, dev and e2e stacks
└── tasks.md
```

### Source Code

```text
src/web/backoffice/
├── package.json, package-lock.json, angular.json, tsconfig*.json, eslint.config.js
├── Dockerfile                    NODE_IMAGE build, NGINX_IMAGE final
├── nginx.conf                    research.md R3
├── compose.yaml                  backoffice-spa, profile apps, health check, no published port
├── .dockerignore
└── src/
    ├── index.html, main.ts, styles.scss
    └── app/
        ├── app.config.ts         router, provideHttpClient(withInterceptors([apiInterceptor])), Material
        ├── app.ts                renders by session state
        ├── session/
        │   ├── session.service.ts      state, load, signIn, signOut, retry
        │   ├── api.interceptor.ts      header, 401, 503
        │   ├── browser-location.ts     injectable wrapper around window.location and history
        │   └── sign-in-errors.ts       code to message
        ├── sign-in/              sign-in screen
        ├── unavailable/          retry screen and banner
        ├── shell/                toolbar, user menu, outlet
        └── home/                 placeholder

src/gateway/Gateway.Api/appsettings.json, src/gateway/compose.yaml    backoffice-spa:8080
deploy/compose.yaml               include src/web/backoffice/compose.yaml
deploy/compose.web-dev.yaml       backoffice-dev and the gateway upstream override
deploy/versions.env               NODE_IMAGE, NGINX_IMAGE, PLAYWRIGHT_IMAGE
scripts/dev.ps1                   -WebDev
scripts/e2e.ps1                   new runner
scripts/smoke-signin.ps1          /backoffice/ host page check
tests/e2e/
├── package.json, package-lock.json, playwright.config.ts, tsconfig.json
├── compose.e2e.yaml
└── specs/backoffice-sign-in.spec.ts
.github/workflows/backoffice.yml (new), e2e.yml (new)
.gitignore                        tests/e2e/test-results, playwright-report, node_modules, dist, .angular
```

**Structure Decision**: Angular CLI layout inside the service-style folder. Session handling is a single folder so it can be copied whole into the storefront.

## Complexity Tracking

No deviations.
