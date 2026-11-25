# Implementation Plan: Backoffice Tenants

**Branch**: `018-backoffice-tenants` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/018-backoffice-tenants/spec.md`

## Summary

The backoffice frame gets a responsive side navigation whose entries depend on the user type (research.md R2). A Tenants page for the platform admin lists tenants from the existing Identity API and opens a create dialog. The dialog uses Signal Forms, whose `submit()` action places the service's field errors and conflicts on their fields (R1, R3–R5). A third end-to-end test covers creating a tenant and signing in as its new admin (R6).

No backend change: the Identity endpoints of 013 are used as they are.

## Technical Context

**Language/Version**: TypeScript 6.0 with Angular 22.3

**Primary Dependencies**: Angular Material 22.3 (sidenav, list, table, dialog, snack-bar, form-field, input), CDK layout, Signal Forms (`@angular/forms/signals`)

**Storage**: none

**Testing**: Vitest with `provideHttpClientTesting`; Playwright end-to-end in `e2e-test`

**Target Platform**: evergreen browsers

**Project Type**: web frontend (existing backoffice app)

**Performance Goals**: none beyond the defaults

**Constraints**: no comments, no icon font or CDN, controls shown only from API flags (principle VI)

**Scale/Scope**: about 8 new source files in `src/web/backoffice`, 1 end-to-end spec

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Only the backoffice folder and one end-to-end spec change |
| VI. Authorization | ✅ | "Create tenant" appears only when `actions.canCreate` is true. Identity still enforces it (platform-only route). The navigation's user-type split is not an authorization decision; it only hides routes the service refuses anyway |
| VII. Tests isolated | ✅ | The new end-to-end spec runs in the existing `e2e-test` setup |
| IX. No comments | ✅ | |
| II, III, IV, V, VIII, X | ✅ n/a or unchanged | |

**Gate: PASS**, with no deviations.

**Post-design re-check**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/018-backoffice-tenants/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/ui.md
└── tasks.md
```

### Source Code

```text
src/web/backoffice/src/app/
├── shell/shell.ts                     sidenav, responsive mode, navigation entries
├── home/home.ts                       link to Tenants for the platform admin
├── app.routes.ts                      'tenants'
├── problems.ts                        problemOf(error)
└── tenants/
    ├── tenants-api.ts                 list(), create()
    ├── tenants-page.ts                list, states, create button, snack bar
    └── create-tenant-dialog.ts        Signal Form, submit() with field errors
tests/e2e/specs/backoffice-tenants.spec.ts
```

**Structure Decision**: a feature folder `tenants/` inside the backoffice app. 019 adds `users/` and `roles/` the same way.

## Complexity Tracking

No deviations.
