# Implementation Plan: Backoffice Staff

**Branch**: `019-backoffice-staff` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/019-backoffice-staff/spec.md`

## Summary

Staff get "Users" and "Roles" in the backoffice navigation.
- The Users page lists staff, with buttons shown only from the API's flags: "Add staff user" (a Signal Forms dialog), "Roles" per row (a dialog with a switch per role, saving each change immediately) and "Disable" per row (with confirmation).
- The Roles page shows each role's permissions. Every display name comes from the API: roles and permissions from `GET /roles`, which gains display names in Identity, and statuses from the 020 lookup. The backoffice holds no lists (R2).
- The staff list gains cursor pagination and filters (email start, status, role) in Identity: one query per page served by a new covering index, an optional total, and a 50,000-row plan test (R9). The backoffice pages with Previous and Next, prefetches the next page, and offers the filters, with status options from the 020 `staff-status` lookup and role options from the roles list (R10). The other staff APIs are unchanged, and the 018 patterns apply (R1–R6). A fourth end-to-end test covers adding, a role, Viewer restrictions and disabling (R7).

## Technical Context

**Language/Version**: TypeScript 6.0 with Angular 22.3

**Primary Dependencies**: Angular Material 22.3 (table, dialog, slide-toggle, snack-bar, form-field, input, select, progress-bar), Signal Forms. Identity: EF Core 10 with Npgsql (existing), a new migration

**Storage**: Identity database: two new indexes (staff list covering index, role assignment index), no new tables

**Testing**: Vitest; Identity integration tests on Testcontainers (paging, filters, a 50,000-row plan and command-count test); Playwright end-to-end in `e2e-test`

**Target Platform**: evergreen browsers

**Project Type**: web frontend (existing backoffice app)

**Performance Goals**: one database query per staff page at any depth, served by an index; "Next" instant through prefetch; no bigger initial bundle (lazy pages)

**Constraints**: no comments; controls only from API flags; fixtures in the real problem shape

**Scale/Scope**: Identity: the ListUsers query, validator, handler, endpoint, 1 migration and tests. Backoffice: about 10 new files. 1 end-to-end spec

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Identity and the backoffice change in one PR but deploy separately. The new `GET /users` answer keeps every old field and adds `nextCursor` and `total`; the only consumer is the backoffice |
| III. Handlers hold logic | ✅ | Paging and filters live in the `ListUsers` handler. No database function (R9) |
| IV. Transactions | ✅ | The list is read-only; the migration only adds indexes |
| V. Multi-tenancy | ✅ | The EF global tenant filter stays in force. The index starts with `tenant_id`. The test proves other tenants never appear |
| Lookups (v1.6.0) | ✅ | Staff status uses the `staff-status` lookup for the filter and the row. Roles are not a lookup: they are defined in code by principle VI and served with display names by `/roles`. The UI holds no lists |
| VI. Authorization | ✅ | Every control follows `canCreate`, `canDisable` and `canAssignRoles`. A 403 is shown as no-access. Identity enforces everything |
| VII. Tests isolated | ✅ | The existing `e2e-test` setup |
| IX. No comments | ✅ | |
| II, VIII, X | ✅ n/a or unchanged | Wolverine code regenerated for the changed endpoint (codegen CI check) |

**Gate: PASS**, with no deviations.

**Post-design re-check**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/019-backoffice-staff/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/ui.md
└── tasks.md
```

### Source Code

```text
src/web/backoffice/src/app/
├── shell/shell.ts                 staff entries Users and Roles
├── app.routes.ts                  lazy users and roles
├── confirm-dialog.ts              reusable confirmation
├── users/
│   ├── users-api.ts               list, create, disable, assign, remove
│   ├── users-page.ts
│   ├── add-staff-dialog.ts
│   └── roles-dialog.ts
├── roles/
│   ├── roles-api.ts
│   └── roles-page.ts
└── lookups/lookups-api.ts         values of a lookup type (020 endpoint)
tests/e2e/specs/backoffice-staff.spec.ts

src/services/identity/
├── Identity.Api/Features/ListUsers/        query with filters and cursor, validator, handler, endpoint
├── Identity.Api/Persistence/               index configuration and migration StaffListIndexes
├── Identity.Api/Internal/Generated/        regenerated Wolverine code
└── Identity.Tests/Users/StaffListPagingTests.cs   filters, cursor, total, isolation, plan and command count
```

**Structure Decision**: feature folders `users/` and `roles/`, next to `tenants/`.

## Complexity Tracking

No deviations.
