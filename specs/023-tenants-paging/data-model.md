# Data Model: Tenants Paging

## Tenant (Identity, table `tenants`)

| Field | Type | Rule |
|---|---|---|
| `Id` | `varchar(32)` | unchanged |
| `Slug` | `varchar(63)`, collation `C` | unchanged rules (unique, check pattern); collation changes to `C` |
| `Name` | `varchar(200)` | unchanged |
| `NormalizedName` | `varchar(200)`, collation `C`, required | new; `Name.ToUpperInvariant()`, set only by `Tenant.Create`; existing rows backfilled with `upper("Name")` |
| `CreatedAt` | `timestamptz` | unchanged |

Indexes: `ux_tenants_slug` (unique, rebuilt with the new collation); `ix_tenants_normalized_name` (new).

## ListTenants (query)

| Field | Type | Rule |
|---|---|---|
| `Q` | string? | trimmed; empty means no filter; at most 200 characters |
| `Size` | int | 1–100; default 25 |
| `After` | string? | a `TenantCursor`; invalid → 422 `invalid-cursor` |
| `IncludeTotal` | bool | default false |

## TenantCursor

| Field | Type | Rule |
|---|---|---|
| `Slug` | string | the last slug of the previous page; encoded as base64url JSON; opaque to clients |

## TenantList (answer)

| Field | Type | Rule |
|---|---|---|
| `Items` | `TenantItem[]` | at most `Size`, ordered by `Slug` in byte order |
| `NextCursor` | string? | null on the last page |
| `Total` | int? | only when `IncludeTotal`; left out of the JSON otherwise |
| `Actions.CanCreate` | bool | unchanged |

`TenantItem` is unchanged: `Id`, `Slug`, `Name`, `CreatedAt`.

## Backoffice

| Name | Type | Rule |
|---|---|---|
| `TenantListRequest` | `{ q: string; size: number; after: string \| null; includeTotal: boolean }` | `q` sent only when not empty after trimming |
| `TenantList` | `{ items, nextCursor, total?, actions }` | as the answer |
| `pageSizes` | `[10, 25, 50]` | default 25; the same as the Users page |
| page state | `not-available` \| `loading` \| `error` \| `loaded` | unchanged kinds; `loaded` now carries the current page, cursors and total |
