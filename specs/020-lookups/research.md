# Research: Lookups

The design follows the owner's decisions of 2026-11-25:
- 1a: the rule covers stored and API values only
- 2a: any signed-in user may read lookups
- 3a: lookups are global per service
- 4a: Identity's user type moves to a lookup
- 5a: this is its own feature, before 019 resumes

The current code was read on the same day: BuildingBlocks 0.3.0, `RouteCategory`, `NoRolesRouteCheck`, Identity's `IdentityDbContext`, `AppUser`, `UserTypes` and `StartupTasks`.

## R1. Where the shared pieces live

**Decision**: a new package, **`BuildingBlocks.Lookups`**, released with the rest of BuildingBlocks as **0.4.0**. It references `BuildingBlocks.AspNetCore` and `BuildingBlocks.EntityFrameworkCore`, and contains:
- the entities `LookupType` and `LookupValue`, with `ModelBuilder.ApplyLookups()` for the EF configuration
- the definitions API: `LookupCatalog`, `LookupTypeDefinition` and `LookupValueDefinition`, with fixed ids
- `LookupSeeder<TDbContext>`
- `LookupReader`: the type list, and the filtered, paged values
- the conditional-GET middleware (R6)

Changes in the existing packages:
- **`BuildingBlocks.AspNetCore`** gains the `SignedIn` route category (R5).
- **`BuildingBlocks.Testing`** gains the route-check changes (R5).

**Why**: principle II allows BuildingBlocks to hold infrastructure only. Generic tables, seeding and reading are infrastructure; the lists themselves are each service's domain and stay in the service. A separate package keeps EF and HTTP pieces out of services that don't use lookups.

**Rejected**:
- **Splitting it across the EF and AspNetCore packages**: the reader and the middleware would need both packages.
- **A shared lookup service**: rejected by principle I and by decision 3a.

## R2. Tables

The tables use the owner's columns, with Identity's naming convention: snake_case tables and PascalCase columns.

| Table | Columns | Constraints |
|---|---|---|
| `lookup_types` | `Id uuid`, `Code varchar(100)`, `Name varchar(200)` | PK `Id`; unique `ux_lookup_types_code` (`Code`); check `ck_lookup_types_code` (`Code ~ '^[a-z0-9]+(-[a-z0-9]+)*$'`) |
| `lookup_values` | `Id uuid`, `Code varchar(100)`, `Name varchar(200)`, `LookupTypeId uuid` | PK `Id`; FK `LookupTypeId` → `lookup_types.Id`, restrict delete; unique `ux_lookup_values_type_code` (`LookupTypeId, Code`); index `ix_lookup_values_type_name` (`LookupTypeId, Name, Id`) for ordered keyset paging; the same code check |

- **No tenant column**: neither entity implements `ITenantOwned`, so `TenantDbContext` adds no filter. Every caller sees the same rows (decision 3a).
- **No sort column**: values are sorted by `Name, Id` (spec assumption).

## R3. Definitions and seeding

- **Definitions**: each service declares a static `LookupCatalog` built from `LookupTypeDefinition(Guid id, string code, string name, params LookupValueDefinition[] values)` and `LookupValueDefinition(Guid id, string code, string name)`.
  - The ids are literal GUIDs in code, so they are the same in every environment.
  - Code reads a value through its definition, for example `UserTypes.Staff.Id` and `UserTypes.Staff.Code`, never through a string typed elsewhere.
  - A unit test checks the catalog: ids unique, codes in the required format, and codes unique per type.
- **Seeding**: `LookupSeeder<TDbContext>.SeedAsync(catalog)` runs in each service's startup tasks right after `MigrateAsync`, and before the host serves requests (`IHostedService.StartAsync` already blocks startup). It runs in one transaction:
  1. Load the existing rows with the catalog's ids.
  2. Insert the missing ones.
  3. Update `Name` where it differs, and also `Code` and type where they differ. Since codes are permanent, that only happens after a hand edit.
  4. Never delete anything.
  5. `SaveChangesAsync`.
- **Concurrency**: when two instances start together, the second insert hits the primary key or `ux_*_code`. The seeder catches the unique violation (`DbUpdateException` carrying `PostgresException` 23505, read through the generic `DbException.SqlState`, so no Npgsql dependency), clears the tracker and runs once more. The rerun finds the rows and changes nothing.
- **Code conflict**: if a hand-made row uses a defined code under another id, the rerun hits the same violation again. Seeding then fails and the service refuses to start, with a log line naming the code. This is FR-004.
- **Start check (FR-004)**: after seeding, the seeder counts the catalog ids in the database. If any is missing, it throws `LookupSeedException` and the start fails.
- **Rejected**:
  - **EF `HasData`**: every name change would need a migration, and EF treats the data as owned by the migration, which conflicts with hand edits being corrected on start.
  - **Raw `INSERT … ON CONFLICT`**: provider-specific SQL in a provider-neutral package.

## R4. Reading and filtering

- **Endpoints**: per service, in its own `Features/Lookups/` slice, as Wolverine queries and handlers (principle III). The handlers call `LookupReader`. The endpoint classes only map the route.
  - No endpoint lists the types (owner, 2026-11-25): types only group values, and screens use the codes they know.
  - `GET /lookups/{typeCode}/values?q&ids&codes&size&after` → `ListLookupValues` → `{ items: [{ id, code, name }], nextCursor }`. An unknown `typeCode` gives 404 `lookup-type-not-found`.
- **Filters**, applied by `LookupReader` with plain LINQ so they stay provider-neutral:
  - **`q`**: trimmed, up to 100 characters. It matches when `Name.ToLower()` or `Code` contains `q.ToLower()`. Npgsql translates this to `lower("Name") LIKE '%…%'`, with the wildcards escaped.
  - **`ids`**: up to 200 ids, as an `IN` list.
  - **`codes`**: up to 200 codes, as an `IN` list.
  - All filters combine with AND.
  - **Keyset paging**: `size` is 1–200 (default 50). `after` is an opaque base64url JSON of `(Name, Id)`, applied as `Name > n OR (Name = n AND Id > i)`, then `ORDER BY Name, Id LIMIT size+1`. This is the 019 pattern.
- **Index**: `ix_lookup_values_type_name` serves the type filter, the ordering and the keyset, so any page costs the same.
  - A contains-search scans the type's rows. That is fine for the short lists of today, and a list that grows large can add a `pg_trgm` index in its service's migration.
  - Rejected for now: a trigram index on every service's database, because nothing needs it yet.
- **Validation**: FluentValidation validators (BuildingBlocks.Validation, existing) check `size` 1–200, at most 200 `ids` and at most 200 `codes`, and `q` up to 100 characters. Failures give 400 `validation-failed`.
  - A malformed `after` gives 400 `validation-failed` on `after`. This is unlike 019's 422: lookups have no domain rule, and the token is just input.
  - The validators run without a permission check first, because lookups have none (R5).
- **Query string shape**: `ids` and `codes` are repeated parameters (`?codes=active&codes=disabled`), which ASP.NET binds to arrays without custom parsing. Comma lists were rejected because a code could later contain characters that need escaping.

## R5. The `SignedIn` route category

- **Category**: a new `RouteCategory.SignedIn`, declared with `[SignedIn]` (`IAuthorizeData` with policy `signed-in`) or `.RequireSignedIn()`.
  - The policy requires an authenticated user whose identity is valid (`CurrentUser.TryCreate`), so the existing malformed-token rules still give 401.
  - It accepts every user type, with or without a tenant, as long as that combination is valid.
- **Exclusive**: `RouteCategories.Of` throws at startup if a route combines `SignedIn` with the allow-list or with platform-only, as it already does for allow-list plus platform-only.
- **Route check**: `NoRolesRouteCheck` puts `SignedIn` routes in a separate `SignedIn` list of the report.
  - It calls each of them **without a token** and expects 401. A 2xx counts as a failure.
  - The tenant and platform routes keep the 403 check.
  - `RouteCheckReport` gains a `SignedIn` collection, an additive change.
- **Why**: lookups have no permission check (decision 2a). Without this category, the no-roles test would fail them, because they answer 200, or they would end up on the anonymous allow-list, which decision 2a rejects.
- **Gateway**: `/api/identity/**` already requires a session at the gateway, so no gateway change is needed. The gateway route-table test stays green.

## R6. Version marker and caching

- **Middleware**: `UseLookupCaching()`, a small middleware from `BuildingBlocks.Lookups`, applies to endpoints carrying `LookupCachingMetadata`, which `.WithLookupCaching()` adds.
  - It buffers the 200 JSON body (small by design) and computes a strong ETag: `"` + base64url(SHA-256(body))[..22] + `"`.
  - It sets `Cache-Control: private, max-age=300` and the `ETag` header.
  - It returns 304 with no body when `If-None-Match` matches (weak comparison, `*` included).
  - It adds `Vary: Cookie, Authorization`.
- **Why middleware, not an endpoint filter**: Wolverine HTTP endpoints are generated, and middleware works the same for any endpoint. ASP.NET Core 10 has no built-in ETag support for minimal APIs, and the common practice is a hand-written conditional GET.
- **Why `private`**: answers travel through the BFF with session cookies, and a shared cache must not store them.
- **Why five minutes**: lookups change only when a service is deployed.
- **Rejected**: a version column, or a hash stored at seeding. Hashing the body is exact and needs no state.

## R7. Identity user type as a lookup

**Catalog**

`IdentityLookups`, in `Identity.Api/Lookups/`, defines two types:

| Type | Values |
|---|---|
| `user-type`, "User type" | `staff` "Staff", `customer` "Customer", `platform` "Platform" |
| `staff-status`, "Staff status" | `active` "Active", `disabled` "Disabled" |

All ids are fixed GUIDs. `UserTypes` becomes a set of `LookupValueDefinition` values. A constant `Code` stays only where C# needs a constant, such as switch patterns and attributes.

**Mapping**

- `AppUser.UserType` stays a `string` code in C#, so the domain and the tokens are unchanged.
- It is mapped to the column `UserTypeId uuid not null`, with an EF value converter from code to id through the catalog.
- LINQ such as `u.UserType == UserTypes.Staff.Code` then translates to `"UserTypeId" = '<staff id>'`, so the queries, the `ClientPolicies` and the seeders keep their shape.
- A code the catalog doesn't know throws in the converter. In practice `AppUser.ForTenant` refuses it first.

**Database rules**

- FK `fk_identity_users_user_type` from `UserTypeId` to `lookup_values.Id`, restrict delete.
- `ck_identity_users_type_tenant` is rewritten with the three ids: the platform id with no tenant, or the staff or customer id with a tenant. That makes it reject any value that is not a `user-type` (FR-010), which a plain FK to `lookup_values` would not, since that also accepts `staff-status` values.
- The filtered unique indexes `ux_identity_users_staff_email` and `ux_identity_users_customer_email` are rewritten on `UserTypeId`.

**Migration `UserTypeLookup`** (hand-edited after EF generates the tables)

1. Create `lookup_types` and `lookup_values`.
2. Insert the `user-type` type and its three values with the catalog ids, using `INSERT … ON CONFLICT DO NOTHING` (SQL inside Identity's own migration is fine).
3. Add `UserTypeId` as nullable, and backfill it: `UPDATE identity_users SET "UserTypeId" = CASE "UserType" WHEN 'staff' THEN … END`.
4. Make it `NOT NULL`.
5. Drop the old check constraint and the old filtered indexes, then the `UserType` column.
6. Create the new check, indexes and FK.
7. `Down` reverses these steps.

**Proof**

- A migration test starts a database at the previous migration and inserts a platform user, staff and a customer with raw SQL. It then migrates and asserts every user's type code and that everything else on the row is unchanged.
- The dev stack's data goes through the same migration with `dev.ps1 up` (never reset).

**Unchanged on the outside**

- the access and identity token claim `user_type`
- `UserRegistered.UserType`
- `ListUsers` `userType`
- the `ClientPolicies` sets

All of them still carry codes.

## R8. Constitution v1.6.0 (MINOR)

- **New rule**, as an Architecture Constraints row: a choice that is stored in a database or exposed by an API comes from the owning service's lookup tables (`lookup_types`, `lookup_values`), not from enums or unchecked text. Enums used only inside code are allowed.
- **Seeding**: lookup data is seeded on every start from code definitions with fixed ids. It never deletes rows, and it is limited to what the service needs.
- **Principle VI** gains the `SignedIn` category: "open to any signed-in user, reviewed, for non-sensitive reference data". The no-roles test checks these routes for 401 when called without a token.
- **Process**: the amendment is its own commit in this PR, made with `/speckit-constitution` before the implementation commits.
