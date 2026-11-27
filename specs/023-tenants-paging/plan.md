# Implementation Plan: Tenants Paging

**Branch**: `023-tenants-paging` | **Date**: 2026-11-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/023-tenants-paging/spec.md`

## Summary

The Tenants list pages, searches and counts like the Users list (019).
- **Identity** (R1–R4):
  - `GET /tenants` takes `q`, `size` (1–100, default 25), `after` and `includeTotal`, and answers `{ items, nextCursor, total?, actions }` sorted by identifier.
  - `Slug` moves to collation `C`.
  - A new `NormalizedName` column (upper-cased name, collation `C`, indexed) serves the case-insensitive name prefix.
  - An invalid `after` is a 422 `invalid-cursor`.
- **Backoffice** (R5–R6):
  - the Tenants page gets the Users page's paging: page sizes, Previous and Next, a background prefetch (022), and the "from–to of N tenants" range
  - an explicit search box (021 style: Enter or the search button)
  - after a tenant is created, the list restarts at the first page with the total
- **e2e** (R7): the tenants spec finds the new tenant by searching for it, because A–Z order may put it off the first row.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (Identity); TypeScript 6.0, Angular 22.3 (backoffice)

**Primary Dependencies**: Wolverine HTTP, EF Core with Npgsql, FluentValidation (`CommandValidator`); Angular Material table, select, form-field, input, icon button. No new packages.

**Storage**: PostgreSQL (Identity database): one migration, which changes the `Slug` collation and adds `NormalizedName` with its index and a backfill.

**Testing**: xUnit v3 with Testcontainers (paging, search, cursor, validation, permission, plan cost); Vitest with `HttpTestingController`; Playwright in `e2e-test`

**Target Platform**: Identity container; evergreen browsers

**Project Type**: web service and web frontend (existing)

**Performance Goals**:
- every page is one query plus an optional count
- no sequential scan
- a deep page costs about the same as the first, at 20,000 tenants

**Constraints**:
- no comments
- explicit search
- never return everything
- platform-only route with its permission check
- the generated Wolverine code is regenerated and committed

**Scale/Scope**:
- Identity: `ListTenants` feature (3 files), `Tenant` entity, DbContext, 1 migration, generated code
- Backoffice: `tenants-api.ts`, `tenants-page.ts` and their specs
- 1 e2e spec

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Identity and backoffice only; no other service changes |
| III. Handlers hold logic | ✅ | The endpoint only maps query parameters to `ListTenants`; the handler filters and pages; `Tenant.Create` sets `NormalizedName` |
| IV. Transactions | ✅ | Read-only handler; no `SaveChangesAsync` |
| V. Multi-tenancy | ✅ | Tenants are not tenant-owned; the route stays platform-only (`[PlatformOnly]`) |
| VI. Authorization | ✅ | `EnsureAsync(TenantsView)` before validation, as today; `canCreate` stays the action flag; the no-roles route test still expects 403 |
| VII. Tests isolated | ✅ | Testcontainers; e2e in `e2e-test` |
| VIII. Docker | ✅ | No image change |
| IX. No comments, X. No attribution | ✅ | |
| Wolverine code generation | ✅ | `scripts/codegen.ps1` regenerates the static handler; the CI guard checks it |
| II. Shared code | ✅ n/a | No BuildingBlocks or contracts change |

**Gate: PASS.** **Post-design re-check**: PASS.

## Project Structure

### Documentation

```text
specs/023-tenants-paging/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/api.md, contracts/ui.md
└── tasks.md
```

### Source Code

```text
src/services/identity/
├── Identity.Api/
│   ├── Tenants/Tenant.cs                          NormalizedName, set in Create
│   ├── Persistence/IdentityDbContext.cs           Slug collation C; NormalizedName column, collation C, index
│   ├── Persistence/Migrations/<ts>_TenantListIndexes.cs
│   ├── Features/ListTenants/ListTenants.cs        query, validator, TenantCursor, TenantList with nextCursor and total
│   ├── Features/ListTenants/ListTenantsEndpoint.cs  q, size, after, includeTotal
│   ├── Features/ListTenants/ListTenantsHandler.cs   filter, keyset, count
│   └── Internal/Generated/                         regenerated
└── Identity.Tests/Tenants/
    ├── ListTenantsTests.cs                        order, paging, search, total, cursor, validation
    ├── TenantListPlanTests.cs                     cost at 20,000 tenants
    ├── TenantListMigrationTests.cs                backfill and rollback
    └── TenantTests.cs                             NormalizedName

src/web/backoffice/src/app/tenants/
├── tenants-api.ts (+spec)     list(request, options)
└── tenants-page.ts (+spec)    paging, search, total, prefetch
tests/e2e/specs/backoffice-tenants.spec.ts   searches for the new tenant
```

**Structure Decision**: the existing `ListTenants` slice and Tenants page change in place. The paging logic is copied from the Users page, not shared, because changing the Users page was not asked for (see research R6).

## Complexity Tracking

No deviations.
