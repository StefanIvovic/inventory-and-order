# Research: BuildingBlocks EF Tenancy

Versions verified on 2026-11-23: Microsoft.EntityFrameworkCore 10.0.15 and
Microsoft.EntityFrameworkCore.Sqlite 10.0.15. Both match the pinned ASP.NET runtime image
10.0.15.

## R1. Where the marker lives

- **Decision**: `ITenantOwned { string TenantId { get; } }` goes in `BuildingBlocks.Core.Tenancy`.
- **Rationale**: Domain entities implement the marker without referencing EF (FR-001). Core keeps
  zero dependencies.

## R2. Adoption shape

- **Decision**: An abstract base class
  `TenantDbContext(DbContextOptions options, ITenantContext tenantContext) : DbContext`. A
  service context derives from it and calls `base.OnModelCreating` (FR-007, SC-003).
- **Alternatives considered**:
  - A `ModelBuilder` extension plus a manually registered interceptor. That is two steps, and
    forgetting one silently disables protection.
  - Overriding `SaveChanges`. Rejected because constitution V names a SaveChanges interceptor,
    and an interceptor also covers every save overload.

## R3. Query filter

- **Decision**: EF Core 10 named query filters. For each entity type whose CLR type implements
  `ITenantOwned` (and is not owned or a keyless view), apply
  `HasQueryFilter(TenantFilter.Name, e => e.TenantId == CurrentTenantId)`, where `Name` is
  `"Tenant"`. `CurrentTenantId` is a property on the context that returns
  `tenantContext.TenantId`.
  - EF parameterizes context members per instance, so a model cached across instances still
    filters by each instance's tenant.
  - When there is no tenant, `CurrentTenantId` is null and the column is required, so the
    predicate matches nothing (fail closed, US1 scenario 4).
- **Rationale**: Named filters allow bypassing the tenant filter only, while service filters
  such as soft delete stay active (FR-003, US3 scenario 3).
- **To verify during implementation**: EF translates interface member access on a generic
  `T : ITenantOwned` inside the filter. The plan is a generic helper invoked per entity type.

## R4. Bypass

- **Decision**: `IQueryable<T>.AcrossAllTenants()` calls
  `IgnoreQueryFilters([TenantFilter.Name])` (FR-004).

## R5. Write guard

- **Decision**: `TenantSaveChangesInterceptor : SaveChangesInterceptor`, added by the base
  context in `OnConfiguring`. In `SavingChanges` and `SavingChangesAsync`, for each
  `ITenantOwned` entry:
  - **Added**:
    - no tenant in context → throw
    - entity tenant empty → stamp it from the context
    - entity tenant differs from the context → throw
  - **Modified** with the TenantId property modified → throw

  The exception is `TenantViolationException : InvalidOperationException`. It is thrown before
  any command is sent to the database, so nothing is persisted (FR-005, SC-002). It surfaces as
  500 through the 002 mapping.
- **Stamping** goes through `entry.Property(nameof(ITenantOwned.TenantId)).CurrentValue`, so
  entities can keep a private setter.

## R6. Column rules

- **Decision**: In the same model loop: `IsRequired()`, `HasMaxLength(64)` and `HasIndex(TenantId)`
  (FR-006). Services add composite indexes as they need them.

## R7. Tests

- **Decision**: A `BuildingBlocks.EntityFrameworkCore.Tests` project using SQLite in memory over
  one open connection per test, with a fresh `TenantContext` and database context per simulated
  request.
- **Test model**:
  - `Order` and `OrderLine`, both tenant-owned, with a navigation between them
  - `Country`, not tenant-owned
  - `Note`, tenant-owned, with an extra named filter `"SoftDelete"`
- **Rationale**: A real relational engine translates the filter to SQL, unlike the in-memory
  provider. It needs no Docker or ports (FR-008, constitution VII).

## R8. Design-time

- **Note for services**: Migration tooling creates contexts without a request. Each service's
  design-time factory passes `new TenantContext()` (no tenant). This is documented in the
  quickstart, and nothing is built here.
