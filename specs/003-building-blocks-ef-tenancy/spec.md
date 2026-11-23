# Feature Specification: BuildingBlocks EF Tenancy

**Feature Branch**: `003-building-blocks-ef-tenancy`

**Created**: 2026-11-23

**Status**: Draft

**Input**: User description: "Feature 003 of Phase 1. EF Core tenancy plumbing in a new shared package: a marker for tenant-owned entities, a global query filter so every query only sees the current tenant's rows, and SaveChanges handling that stamps TenantId on new rows and refuses cross-tenant writes. Builds on the tenant context from 002."

## User Scenarios & Testing *(mandatory)*

The users are the developers of every service that stores tenant-owned data.

### User Story 1 - Reads only ever see the current tenant (Priority: P1)

A developer marks an entity as tenant-owned. Every query against it, including lookups by id, lists, counts and joins, returns only rows of the tenant in the current tenant context. The developer never writes a tenant condition by hand.

**Why this priority**: This is the core multi-tenancy guarantee (constitution V). A single missing `WHERE TenantId = ...` would leak another company's data.

**Independent Test**: Seed rows for `acme` and `globex`. With the tenant context set to `acme`, every query shape returns only `acme` rows, and looking up a `globex` row by id finds nothing.

**Acceptance Scenarios**:

1. **Given** rows for `acme` and `globex`, **When** the tenant context is `acme` and the code lists all entities, **Then** only `acme` rows are returned.
2. **Given** a `globex` row's id, **When** the tenant context is `acme` and the code looks it up by id, **Then** nothing is found, which callers turn into a 404 via `NotFoundException`.
3. **Given** a query that joins or includes another tenant-owned entity, **When** it runs for `acme`, **Then** related rows from other tenants are excluded too.
4. **Given** no tenant in the context (for example a platform request without a tenant), **When** any tenant-owned entity is queried, **Then** no rows are returned (fail closed).
5. **Given** an entity that is not marked tenant-owned, **When** it is queried, **Then** no tenant filter is applied.

---

### User Story 2 - Writes are stamped and guarded (Priority: P1)

When a new tenant-owned entity is saved, its tenant is filled in from the tenant context automatically. Any attempt to save data for another tenant, or without a tenant, is refused. Nothing is written in that case.

**Why this priority**: Reads are only safe if writes can never put data under the wrong tenant.

**Independent Test**: Save new entities with the tenant context set and unset, and with a mismatching tenant. Then modify an existing entity's tenant. Check what is persisted.

**Acceptance Scenarios**:

1. **Given** the tenant context `acme`, **When** a new tenant-owned entity without a tenant is saved, **Then** it is stored with tenant `acme`.
2. **Given** the tenant context `acme`, **When** a new entity explicitly assigned tenant `acme` is saved, **Then** it is stored.
3. **Given** the tenant context `acme`, **When** a new entity explicitly assigned tenant `globex` is saved, **Then** the save fails and nothing from that save is persisted.
4. **Given** no tenant in the context, **When** a new tenant-owned entity is saved, **Then** the save fails and nothing is persisted.
5. **Given** an existing `acme` entity, **When** code changes its tenant and saves, **Then** the save fails, because the tenant of a row is immutable.
6. **Given** a save that mixes valid entities with one invalid one, **When** it runs, **Then** the whole save fails and none of the entities are persisted.

---

### User Story 3 - Deliberate cross-tenant access for platform operations (Priority: P2)

A platform operation, such as an admin report across all companies, can explicitly opt out of the tenant filter for one query. The opt-out is visible in code and limited to that query.

**Why this priority**: Rare but legitimate. It must be explicit rather than accidental. It is P2 because no service needs it in Phase 1.

**Independent Test**: With the tenant context `acme`, a query that explicitly opts out returns rows of all tenants, and the next normal query is filtered again.

**Acceptance Scenarios**:

1. **Given** rows for `acme` and `globex`, **When** a query explicitly opts out of tenant filtering, **Then** it returns rows of both tenants.
2. **Given** the opt-out was used on one query, **When** the next query runs without it, **Then** it is filtered to the current tenant.
3. **Given** an entity type that also has another service-defined filter (for example soft delete), **When** a query opts out of tenant filtering only, **Then** the other filter still applies.

---

### User Story 4 - One-line adoption by a service (Priority: P2)

A service's database context gets the whole behaviour of US1–US3 with one base type or registration step and no per-entity configuration beyond the marker.

**Why this priority**: The behaviour must be identical in every service and impossible to forget for a new entity.

**Independent Test**: A test database context built only from the package, with two entity types marked tenant-owned and one not, shows US1–US3 behaviour with no extra configuration.

**Acceptance Scenarios**:

1. **Given** a database context adopting the package, **When** a new entity type is marked tenant-owned, **Then** it is filtered, stamped and guarded with no further code.
2. **Given** a tenant-owned entity, **When** the model is built, **Then** its tenant column is required and indexed.

### Edge Cases

- An entity is created in one tenant context and the same database-context instance is reused for another tenant. This can't happen, because database contexts are scoped per request or message and the tenant context is set once (feature 002).
- Raw SQL queries bypass the filter unless they are composed with LINQ on the entity set. Raw SQL against tenant-owned tables is out of scope and discouraged.
- Bulk update and delete operations (executed directly in the database) respect the query filter for their selection. The tenant column must not be changed through them, and this is documented for developers.
- An existing tenant-owned entity is deleted in a context with a different tenant. It can't be loaded in the first place, because the filter hides it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The core package (no database dependency) MUST define the marker for tenant-owned entities, exposing a read-only tenant identifier, so domain models can use it without referencing the database package.
- **FR-002**: A new package MUST apply a tenant query filter to every entity marked tenant-owned. The filter limits results to the current tenant context, and returns nothing when the context has no tenant.
- **FR-003**: The tenant filter MUST be identifiable, so it can be bypassed on its own while other filters on the same entity stay active.
- **FR-004**: The package MUST offer one explicit, per-query way to bypass only the tenant filter.
- **FR-005**: On save, the package MUST:
  - stamp the tenant on new tenant-owned entities that have none
  - reject new entities whose tenant differs from the context
  - reject new entities when the context has no tenant
  - reject any change to the tenant of an existing entity

  A rejection fails the whole save with a clear error, and nothing from that save is persisted.
- **FR-006**: For every tenant-owned entity the model MUST make the tenant column required, with a maximum length of 64, and indexed.
- **FR-007**: Adoption MUST require only deriving the service's database context from the package's base context (or one equivalent registration) plus marking the entities.
- **FR-008**: The behaviour MUST be verified against a real relational database engine in tests that run without Docker. The package MUST NOT depend on a specific database provider.
- **FR-009**: The package MUST be produced by the existing pack pipeline under the `building-blocks/v` prefix, and its tests MUST run in the existing CI workflow.

### Key Entities

- **Tenant-owned entity**: any persisted entity whose rows belong to exactly one tenant. It has a tenant identifier that is set once and never changes.
- **Tenant filter**: the named rule that restricts queries to the current tenant.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across all tested query shapes (list, lookup by id, count, include and join, projection), zero rows of another tenant are ever returned.
- **SC-002**: 100% of invalid writes (wrong tenant, no tenant, changed tenant) are rejected, with zero rows persisted from the rejected save.
- **SC-003**: Adopting the package in a database context takes one base-type change, and each new tenant-owned entity takes one marker. Zero per-entity filter code.
- **SC-004**: All acceptance scenarios are automated and pass in CI without Docker.

## Assumptions

- The rejection error is an infrastructure error (a programming mistake, not a business rule), so it surfaces as 500 through the 002 error mapping, not as 409 or 422.
- Soft delete and other filters are service concerns. This feature only guarantees that the tenant filter can be bypassed on its own.
- Tests use an in-process relational engine (SQLite in memory). Production uses PostgreSQL, which arrives with the first service.
