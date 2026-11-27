# Research: Tenants Paging

Read on 2026-11-27 from the repository: the 019 staff list (`Features/ListUsers`, migration `StaffListIndexes`, research R8–R9), the current `ListTenants` slice, `Tenant`, `IdentityDbContext`, the Tenants page, and the 021/022 backoffice patterns.

## R1. Order and keyset

- **Decision**:
  - order by `Slug` (unique), in collation `C`
  - the cursor is base64url JSON of `{ "slug": … }`
  - the next page is `Slug > after`
  - take `size + 1` rows; the extra row means there is a next page
- **Why**:
  - slugs are unique, so one column is a total order and the keyset condition is a plain index range
  - 019 R8 showed that `OR` keyset conditions defeat the index; a single column avoids that
  - `C` makes ordering, `>` and `LIKE 'x%'` all use the same B-tree; slugs are lowercase ASCII, digits and hyphens, so byte order is the natural A–Z
- **Migration**: `AlterColumn` with `collation: "C"`, as 019 did for `NormalizedEmail`. Postgres rebuilds `ux_tenants_slug` as part of the change.
- **Rejected**:
  - newest first: it needs a new `(CreatedAt, Id)` index, and the owner chose A–Z
  - `(Slug, Id)` keyset: not needed, because `Slug` is unique

## R2. Search: identifier or name prefix, ignoring case

- **Identifier**: `Slug LIKE lower(q) || '%'`. Slugs are always lowercase (check constraint), so lower-casing the text is enough.
- **Name**:
  - a new column, `NormalizedName`: `varchar(200)`, required, collation `C`, `Name.ToUpperInvariant()`, set by `Tenant.Create`
  - an index `ix_tenants_normalized_name` on it
  - the filter is `NormalizedName LIKE upper(q) || '%'`
  - this mirrors ASP.NET Identity's `NormalizedEmail`, which 019 already searches the same way
- **Wildcards**: the filters use `StartsWith` with a parameter. EF Core with Npgsql escapes `%`, `_` and `\` in it, so they match literally, as in the 019 email search.
- **Combined**: `(Slug starts with s) OR (NormalizedName starts with n)`. Postgres can combine the two index scans (BitmapOr) or walk the slug index with a filter; the plan test (R4) checks the cost either way.
- **Text**: trimmed. Empty or whitespace means no filter. At most 200 characters (`Tenant.NameMaxLength`).
- **Backfill**: the migration sets `NormalizedName = upper("Name")` for existing rows.
  - Postgres `upper()` and .NET `ToUpperInvariant` agree for ASCII
  - existing names (the seeded demo tenant and test data) are ASCII
  - new rows always come from `Tenant.Create`
- **Rejected**:
  - `ILIKE` or `lower("Name")` without a stored column: `ILIKE` cannot use a B-tree, and an expression index puts Postgres casing on one side and .NET casing on the other
  - "contains" search: needs `pg_trgm`, which is the deferred trigram follow-up

## R3. Query, answer and errors

- **Query**: `ListTenants(Q, Size = 25, After, IncludeTotal)`.
- **Validator**: `ListTenantsValidator : CommandValidator<ListTenants>` checks `Size` 1–100 and `Q` at most 200 characters. The handler checks the permission first, then validates, as `ListUsers` does.
- **Endpoint**: `[WolverineGet("/tenants")] [PlatformOnly]` with `q`, `size`, `after` and `includeTotal` query parameters, mapped to the query.
- **Answer**: `TenantList(Items, NextCursor, Total, Actions)`.
  - `Total` is left out when null (`JsonIgnore WhenWritingNull`), as in `UserList`.
  - `TenantItem` is unchanged (`id`, `slug`, `name`, `createdAt`).
- **Total**: `CountAsync` over the filtered query, only when `IncludeTotal`.
- **Invalid cursor**: `TenantCursor.Decode` throws `DomainException("invalid-cursor", Rule)`, which maps to 422, the same code as the staff list.
- **Index-only page**: the page selects `Id`, `Slug`, `Name` and `CreatedAt`. The table is small per row, so no `INCLUDE` columns are added; the plan test decides whether that is enough.

## R4. Plan cost test

- **Decision**: `TenantListPlanTests` seeds 20,000 tenants with bulk SQL, then, for no filter, `q=t-1` (identifier) and `q=NAME 1` (name), checks:
  - each page is one command, with no sequential scan
  - a deep page costs no more than twice the first
- **Recording**: the same command recorder and `EXPLAIN (ANALYZE, BUFFERS)` approach as `StaffListPlanTests`.
- **Why**: the owner's rule for lists is that they stay fast at any size; 019 R8 found the plan problems only through this test.

## R5. Backoffice API and page

- **API**: `TenantsApi.list(request, options)`, where `request = { q, size, after, includeTotal }` and `options` is `RequestOptions` (022).
  - `q` is trimmed and sent only when not empty; `after` and `includeTotal` only when set.
  - `TenantList` gains `nextCursor` and `total?`.
- **Page**:
  - **Sizes**: `pageSizes = [10, 25, 50]`, default 25, as on the Users page (R8).
  - **Paging state**: a list of cursors, a cache of pages keyed by search, size and cursor, and a generation counter that ignores stale answers. This is the Users page's logic.
  - **Previous and Next**: Previous shows a cached page; Next shows the prefetched page or asks for it.
  - **Prefetch**: after a page shows, the next page is fetched with `{ background: true }`; a failed prefetch is ignored.
  - **Search**: a text field with a search icon button (`aria-label="Search tenants"`), run on Enter or the button.
  - **Restarts**: a search, a page size change or a created tenant restarts at the first page with `includeTotal`. An `invalid-cursor` answer also restarts.
  - **Messages**:
    - no tenants and no search: "No tenants yet."
    - no match: "No tenants match this search." with "Clear search"
  - **Unchanged**: the first-load bar, error and "Try again" states, `canCreate`, and the not-available state for non-platform users.
  - The client-side sort by `createdAt` is removed; the order comes from the service.

## R6. Copy, not share, the paging logic

- **Decision**: the Tenants page has its own copy of the Users page's paging code.
- **Why**: a shared pager would mean refactoring the Users page, which this feature was not asked to change. The duplication is about 80 lines in one place.
- **Follow-up**: a shared keyset pager for both pages, if the owner wants it, as its own feature.

## R7. End-to-end

- **Decision**: in `backoffice-tenants.spec.ts`, after creating the tenant, search for its slug and check the first row.
- **Why**: in A–Z order, the seeded `demo` tenant sorts before `e2e-…`, so the new tenant is no longer the first row. The search also exercises US2 in a real browser.

## R8. Page sizes

- **Decision (owner, 2026-11-27)**: 10, 25 and 50, default 25, the same as the Users page (019).
- The service still accepts 1–100, like the staff list, so a client can ask for more, but never for everything.

## R9. Findings during implementation (2026-11-27)

- **The slug index is `ux_tenants_slug`**, not `ix_tenants_slug`. The docs and the migration test use the real name.
- **Migration order**:
  - EF generated `NormalizedName` as a required column with a `''` default
  - the migration instead adds it as nullable, backfills it with `upper("Name")`, then makes it required, so no empty default stays on the column
- **Generated code during the work**:
  - the committed static Wolverine code is compiled in every build
  - so the handler's new validator broke the build until `scripts/codegen.ps1 identity` ran (in T004, not only in polish)
- **A rare match does not read the list**:
  - a search that matches one tenant out of 20,000 is answered from the indexes, by identifier and by name, in at most 50 blocks for the count and the page
  - a test covers this next to the deep-page test
- **Upper-casing follows .NET invariant rules**:
  - the search text and `NormalizedName` are both upper-cased with `ToUpperInvariant`, so "i" becomes "I", never "İ"
  - a name with "İ" is found by typing "İ"; the tests show this
- **Tests that passed when written**:
  - the client tests in `tenants-api.spec.ts` were written together with the client
  - the T009 test passed at once, because restarting already kept the search and the size
