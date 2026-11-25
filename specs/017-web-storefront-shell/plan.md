# Implementation Plan: Storefront Shell

**Branch**: `017-web-storefront-shell` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/017-web-storefront-shell/spec.md`

## Summary

A second Angular 22 app in `src/web/storefront/`, generated and cleaned like the backoffice (016) and served by nginx under `/shop/` behind the gateway.
- A route matcher accepts only valid shop identifiers; anything else shows "shop not found" (research.md R2).
- The session module is a copy of the backoffice one, tied to the shop identifier and `X-Bff-App: storefront`. Visitors see the full frame; signed-in customers get an account menu and an Account page (R3).
- `-WebDev` gains a storefront dev server (R5).
- The storefront has its own CI workflow, and a second end-to-end test registers a customer (R6, R7).

## Technical Context

**Language/Version**: TypeScript 6.0 with Angular 22.3; nginx configuration; PowerShell

**Primary Dependencies**: as 016: Angular 22.3 (core, router, Material, CDK), angular-eslint 22.8.0, Vitest with jsdom, `@playwright/test` 1.65.1

**Storage**: none in the browser

**Testing**: Vitest unit tests; Playwright end-to-end in `e2e-test`

**Target Platform**: evergreen browsers; the same pinned Node, nginx and Playwright images as 016

**Project Type**: web frontend (SPA)

**Performance Goals**: live reload under 5 seconds

**Constraints**: the same as 016 (no comments, no OIDC library, no CDN, no shared code, tests isolated). Shop identifiers follow Identity's rule exactly.

**Scale/Scope**: about 12 app source files, the nginx config, Dockerfile, compose file, a web-dev addition, 1 end-to-end spec, 1 workflow, a one-line gateway change

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Own folder, build context and workflow on `src/web/storefront/**` |
| II. Shared code via packages | ✅ | Nothing shared. The session module and nginx config are copies (owner decision in 016). The shop identifier rule is copied as well, as the gateway does |
| V. Multi-tenancy | ✅ | The shop comes from the URL, only to address the gateway's per-shop endpoints. The tenant comes from the session. A session for another shop is shown as a visitor |
| VI. Authorization | ✅ | The app decides nothing; the account page shows only the session's own data |
| VII. Tests isolated | ✅ | Unit tests need no infrastructure. The end-to-end test reuses the 016 `e2e-test` setup with the same teardown and cleanup checks |
| VIII. Docker | ✅ | The same pinned images. The dev server runs in Docker |
| IX. No comments | ✅ | Generated comments are removed (016 R11) |
| III, IV, X | ✅ n/a | |

**Gate: PASS**, with no deviations.

**Post-design re-check**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/017-web-storefront-shell/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/ui.md          screens, texts, routes, navigation and HTTP rules
├── contracts/serving.md     paths, headers, dev and e2e additions
└── tasks.md
```

### Source Code

```text
src/web/storefront/
├── package.json, package-lock.json, angular.json, tsconfig*.json, eslint.config.js, .editorconfig, .prettierrc
├── Dockerfile, nginx.conf, security-headers.conf, compose.yaml (storefront-spa), .dockerignore
└── src/
    ├── index.html (base /shop/), main.ts, styles.scss
    └── app/
        ├── app.config.ts, app.routes.ts, app.ts
        ├── shop.ts                     slug pattern, matcher, slug from path
        ├── session/                    session.service, api.interceptor, browser-location, sign-in-errors, testing/
        ├── frame/                      header (title, Sign in or account menu), banners, outlet
        ├── home/                       placeholder
        ├── account/                    account page
        └── shop-not-found/

src/gateway/Gateway.Api/appsettings.json, src/gateway/compose.yaml    storefront-spa:8080
deploy/compose.yaml                 include src/web/storefront/compose.yaml
deploy/compose.web-dev.yaml         storefront-dev and the gateway override
scripts/smoke-signin.ps1            /shop/demo/ host page check
tests/e2e/specs/storefront-register.spec.ts
.github/workflows/storefront.yml
README.md, deploy/README.md
```

**Structure Decision**: the same layout as the backoffice. `frame/` replaces `shell/`, since visitors see it too.

## Complexity Tracking

No deviations.
