# Feature Specification: Permission Invalidation

**Feature Branch**: `007-permission-invalidation`

**Created**: 2026-11-23

**Status**: Draft

**Input**: User description: "Feature 007 of Phase 1. When a service changes a user's role assignments, every running instance of that service must drop its cached permissions for that user, and only after the change is committed, so no instance can re-cache the old permissions. This is done through a PermissionAssignmentsChanged event that is broadcast to all instances of the same service. Plus the constitution rule that this event is the only way to invalidate, and the constitution fixes for the renamed handler base types from feature 006."

## User Scenarios & Testing *(mandatory)*

The users are the developers of every service, and indirectly the tenant admins whose role changes must take effect.

### User Story 1 - A role change takes effect on every instance (Priority: P1)

A tenant admin changes a user's roles in a service that runs as several instances. The user's next request sees the new permissions, whichever instance serves it.

**Why this priority**: Without it, a removed permission keeps working on the other instances for up to the cache duration (60 seconds), and an added one is missing there.

**Independent Test**: Two instances of one service share a database. Both cache a user's permissions. The role change runs on instance A. Both instances then reload the permissions from the database and see the new set.

**Acceptance Scenarios**:

1. **Given** instances A and B have both cached user `u1`'s permissions, **When** a role change for `u1` commits on A, **Then** the next permission check for `u1` on A and on B reads the new permissions.
2. **Given** the same setup, **When** the change concerns user `u2`, **Then** `u1`'s cached permissions remain cached on both instances.
3. **Given** two different services, X and Y, **When** a role change commits in X, **Then** Y's caches are untouched, because each service owns its own permissions.
4. **Given** an instance that starts after the change was committed, **When** it checks permissions, **Then** it reads from the database. A new instance has an empty cache, so nothing is missed.

---

### User Story 2 - Invalidation only after commit (Priority: P1)

The cache is dropped only once the role change is durably committed. A change that fails or rolls back invalidates nothing. There is never a window in which an instance reloads the old assignments and caches them as fresh.

**Why this priority**: Invalidating before the commit lets a concurrent request reload the old data and cache it again for the full duration. That is the exact defect identified during the design.

**Independent Test**: A role-change handler that fails after making its change. Caches stay warm and the provider is not called again. In the successful case, the reload returns the committed data.

**Acceptance Scenarios**:

1. **Given** a role-change handler that fails after changing the assignments, **When** it runs, **Then** no instance's cache is invalidated and the assignments are unchanged.
2. **Given** a successful role change, **When** any instance next reloads permissions after the invalidation, **Then** it sees the committed change.
3. **Given** the broker is briefly unavailable when the change commits, **When** it recovers, **Then** the invalidation is still delivered, because it goes through the outbox.

---

### User Story 3 - One rule, enforced by design (Priority: P2)

Developers have exactly one way to invalidate permissions: return the change event from the handler that changed the assignments. The constitution states this. Direct invalidation calls are reserved for the infrastructure that handles the event.

**Why this priority**: The rule only works if nobody bypasses it. It is P2 because US1 and US2 deliver the behaviour.

**Independent Test**: The constitution contains the rule. The quickstart shows the only supported pattern, and the feature's own handler is the only production caller of the invalidation API.

**Acceptance Scenarios**:

1. **Given** the constitution, **When** it is read, **Then** it states that permission caches are invalidated only by publishing the change event after commit, and never by calling the cache directly from a handler.
2. **Given** a service enabling permission invalidation, **When** it is configured, **Then** one setup call wires the publishing, the per-instance listening and the handling.

### Edge Cases

- An instance is shut down or crashes. Its per-instance listening queue disappears with it, and nothing accumulates on the broker for dead instances.
- The same change event is delivered twice. Invalidating twice is harmless.
- A role change for a platform user without a tenant. The event carries no tenant, and the matching cache entry is invalidated.
- Many role changes in one handler. One event per affected user, in the same commit.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The messaging package MUST define the change event, carrying the tenant (optional) and the user id, with a stable versioned identity.
- **FR-002**: The messaging package MUST provide one setup call that, for the current service:
  - publishes the change event to a service-specific broadcast destination
  - gives every running instance its own short-lived listening queue bound to that destination
  - registers the handler that invalidates the local permission cache
- **FR-003**: The invalidation MUST happen only after the change is committed, by publishing the event through the transactional outbox from the handler that changed the assignments.
- **FR-004**: Each instance's listening queue MUST be removed automatically when the instance stops, and MUST NOT be shared between instances.
- **FR-005**: Invalidation events of one service MUST NOT reach other services.
- **FR-006**: The constitution MUST be amended (minor version):
  - add the invalidation rule
  - replace the outdated handler names in principle III (`EventHandler<TDbContext>` becomes `MessageHandler<TDbContext>`, and the message context becomes `IEventContext`)
  - record the Wolverine code-generation decision: runtime generation during development and tests, pre-generated code in production
- **FR-007**: All behaviour MUST be tested against real PostgreSQL and RabbitMQ containers, with several instances of one service, and nothing left behind.

### Key Entities

- **Permission assignments changed**: the fact that one user's role assignments in one tenant of one service changed.
- **Instance listening queue**: a per-process, auto-deleting queue that receives every invalidation of its service.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a committed role change, 100% of running instances of the service reload that user's permissions on the next check.
- **SC-002**: After a failed role change, 0 instances reload, and the cached permission sets are unchanged.
- **SC-003**: 0 invalidations cross into another service.
- **SC-004**: After the test run, 0 per-instance queues remain on the broker for stopped instances.

## Assumptions

- Invalidating on Identity's `UserDisabled` is deferred to feature 011, when Identity publishes it. The token's 15-minute lifetime already bounds the exposure.
- The broadcast destination per service is a fanout exchange named after the service, so services stay isolated.
- Per-instance queues are non-durable, exclusive and auto-deleting. They do not require a durable inbox, because invalidation is idempotent and a missed one is harmless for a new instance, which starts with an empty cache. They MUST NOT use the durable inbox: instances share one database, so a shared inbox would discard every instance's copy but the first (research.md R4a).
