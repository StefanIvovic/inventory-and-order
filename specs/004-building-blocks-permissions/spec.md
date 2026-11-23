# Feature Specification: BuildingBlocks Permissions

**Feature Branch**: `004-building-blocks-permissions`

**Created**: 2026-11-23

**Status**: Draft

**Input**: User description: "Feature 004 of Phase 1. Explicit permission checks through an injected IPermissionManager (no attributes), backed by each service's own permission provider over its own database, with a short cache that the service can invalidate immediately. A denied check becomes a 403 response listing the missing permissions. Plus the CommandHandler base class that gives handlers the current user, permissions, tenant, clock and their database context."

## User Scenarios & Testing *(mandatory)*

The users are the developers of every service, writing command handlers and screen queries.

### User Story 1 - Explicit permission checks in handlers (Priority: P1)

A handler asks the permission manager whether the current user has a permission, all of several, or any of several. It can also demand one or all, failing if they are missing. The manager gets the user's permissions from the service's own provider, so each service decides what its roles mean (constitution VI).

**Why this priority**: Every protected operation in every service depends on this. It is the only permission mechanism, because attributes and policies are not used.

**Independent Test**: With a provider that grants `inventory.view` and `inventory.adjust`, each check returns the expected answer, and demanding a missing permission fails with the missing names.

**Acceptance Scenarios**:

1. **Given** a user granted `inventory.view` and `inventory.adjust`, **When** the handler checks `inventory.adjust`, **Then** the answer is yes.
2. **Given** the same user, **When** it checks all of [`inventory.adjust`, `inventory.transfer`], **Then** the answer is no. **When** it checks any of the same list, **Then** the answer is yes.
3. **Given** the same user, **When** it demands `inventory.transfer`, **Then** the check fails and reports `inventory.transfer` as missing.
4. **Given** the same user, **When** it demands all of [`inventory.view`, `inventory.transfer`, `inventory.audit`], **Then** the failure reports exactly `inventory.transfer` and `inventory.audit` as missing.
5. **Given** a handler that needs the full granted set to compute screen action flags, **When** it asks for the granted permissions, **Then** it gets the complete set.
6. **Given** an unauthenticated caller, **When** any check runs, **Then** the user has no permissions, and the provider is not called.

---

### User Story 2 - Denied checks become a standard 403 (Priority: P1)

When a demanded permission is missing, the client gets a 403 error response in the standard shape, with code `permission-denied` and the list of missing permissions. The UI can explain why, and support can diagnose it.

**Why this priority**: The constitution requires a denied check to produce a 403 that lists the missing permissions, and the 403 safety-net test in every service relies on it.

**Independent Test**: An endpoint whose handler demands a missing permission returns 403 with the expected body.

**Acceptance Scenarios**:

1. **Given** a handler demands `orders.cancel` and the user lacks it, **When** the response is produced, **Then** it has status 403, code `permission-denied`, `missingPermissions: ["orders.cancel"]` and a trace id.
2. **Given** a denied request, **When** the service log is inspected, **Then** a warning names the missing permissions, the user id and the trace id.
3. **Given** a denied request, **When** the handler threw before changing anything, **Then** nothing is persisted. This is guaranteed by the transaction rules in feature 005, and is documented here only.

---

### User Story 3 - Fast lookups with immediate invalidation (Priority: P1)

Permission lookups are cached briefly per user and tenant, so the service database is not queried on every check. When the service changes a user's role assignment, it invalidates that user's entry, and the next request sees the change immediately.

**Why this priority**: Without the cache, every request costs at least one extra query. Without invalidation, a removed permission keeps working until the cache expires.

**Independent Test**: Count provider calls across repeated checks, across requests, after invalidation, and after the cache duration passes.

**Acceptance Scenarios**:

1. **Given** several checks within one request, **When** they run, **Then** the provider is called at most once.
2. **Given** two requests by the same user and tenant within the cache duration, **When** both run checks, **Then** the provider is called once in total.
3. **Given** a cached entry, **When** the service invalidates that user and tenant, **Then** the next request calls the provider again and sees the new permissions.
4. **Given** a cached entry, **When** the cache duration (default 60 seconds) has passed, **Then** the next request calls the provider again.
5. **Given** the same user id in two different tenants, or two different users in the same tenant, **When** checks run, **Then** their cache entries never mix.

---

### User Story 4 - Command handler base with request context (Priority: P2)

A command handler derives from one base type and immediately has the current user, the permission manager, the tenant context, the clock and its database context available. It needs no repeated constructor boilerplate.

**Why this priority**: It keeps every handler uniform (constitution III). It is P2 because handlers could inject the same services directly.

**Independent Test**: A handler derived from the base, resolved for an authenticated request, can read the user, tenant and clock, run permission checks, and reach its database context.

**Acceptance Scenarios**:

1. **Given** a handler derived from the command base, **When** it runs for staff user `u1` in tenant `acme`, **Then** it sees user `u1`, tenant `acme`, a working permission manager and its database context.
2. **Given** a test that replaces the clock, **When** the handler reads the current time, **Then** it gets the replaced time.
3. **Given** a handler for messages from other services (feature 005), **When** it is written, **Then** it does not use the command base, so it has no access to permissions. This is enforced by feature 005's separate event base.

### Edge Cases

- The provider throws, for example because the database is unavailable. The error propagates as a 500, and nothing is cached.
- The provider returns permissions with different letter case. Permission names are compared exactly (ordinal). Services define them as constants.
- A platform user without a tenant. The cache key uses an empty tenant segment, and the service's provider decides what platform users may do.
- An invalidation request for a user with no cached entry does nothing.
- The service configures the application-wide memory cache with a size limit, or uses it for other data. The permission cache is unaffected, because it uses its own private store.
- Demanding an empty list of permissions succeeds trivially.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The core package MUST define:
  - the permission manager abstraction: has one, has all, has any, ensure one, ensure all, get granted set
  - the provider abstraction that each service implements: the granted set for a given current user
  - the cache-invalidation abstraction: invalidate by tenant and user
  - the permission-denied error, carrying the missing permissions
- **FR-002**: Ensure operations MUST fail with the permission-denied error listing exactly the missing permissions, in the order requested.
- **FR-003**: Unauthenticated callers MUST have an empty granted set, and the provider MUST NOT be called for them.
- **FR-004**: Granted sets MUST be cached per (tenant, user) for a configurable duration (default 60 seconds), and resolved at most once per request. Invalidation MUST take effect for the next request. Provider failures MUST NOT be cached. The cache MUST use its own private store, isolated from any application-wide cache configuration.
- **FR-005**: The error mapping from feature 002 MUST map the permission-denied error to 403 with code `permission-denied`, a `missingPermissions` array and a trace id. It is logged at warning level with the user id and the missing permissions.
- **FR-006**: The core package MUST define a request context abstraction exposing current user, tenant context, permission manager and clock, and a command-handler base type that exposes the request context, the user, the permissions and a typed database context.
- **FR-007**: Adoption MUST be one registration that names the service's provider type, on top of the existing `AddBuildingBlocks()`. The clock defaults to the system clock and can be replaced in tests.
- **FR-008**: Everything MUST be covered by automated tests that run without Docker. Time-dependent behaviour MUST be tested with a controllable clock, never with real waiting.
- **FR-009**: Out of scope: the message-handler base and message context (feature 005), the per-service no-roles 403 route test (added with each service), and authentication setup (feature 007).

### Key Entities

- **Permission**: a stable name such as `inventory.adjust`, defined in the owning service's code.
- **Granted set**: all permissions one user has in one tenant, in one service.
- **Permission provider**: the service-owned source of granted sets.
- **Request context**: the bundle a command handler works with (user, tenant, permissions, clock).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of denied ensure operations produce a 403 with the exact list of missing permissions.
- **SC-002**: Repeated checks by one user within the cache duration cost exactly one provider call, and after invalidation exactly one more.
- **SC-003**: A service adopts permissions with one additional registration line plus its provider class.
- **SC-004**: Every acceptance scenario is automated and passes in CI without Docker or real waiting.

## Assumptions

- Implementations live in the existing `BuildingBlocks.AspNetCore` package, and the abstractions and handler base in `BuildingBlocks.Core`. No new package is needed.
- The cache is per service instance (in memory). A distributed cache arrives with Redis in phase 5, as decided.
- Unauthenticated requests are normally stopped before handlers by the authentication setup in feature 007. Here they simply have no permissions, which gives 403.
