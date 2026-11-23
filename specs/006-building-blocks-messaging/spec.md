# Feature Specification: BuildingBlocks Messaging

**Feature Branch**: `006-building-blocks-messaging`

**Created**: 2026-11-23

**Status**: Draft

**Input**: User description: "Feature 006 of Phase 1. Shared Wolverine setup for every service:
- one transaction per handler, committing entity changes together with outgoing messages (outbox), and rolling everything back on failure
- deduplication of incoming messages (inbox)
- explicit RabbitMQ routing per integration event, using stable message identities
- tenant, initiating user and correlation id carried through message headers, so event handlers run in the right tenant
- the EventHandler base class (tenant and message context, no permissions)
- verifying the open Wolverine points: handler discovery on derived classes, HTTP mapping with response plus cascaded events, header propagation

Permission-cache invalidation is feature 007, and validation is feature 008."

## User Scenarios & Testing *(mandatory)*

The users are the developers of every service.

### User Story 1 - All or nothing per handler (Priority: P1)

A handler changes data and returns events. If it succeeds, the data changes and the events are committed together, and the events are delivered only after the commit. If anything fails, nothing is saved and nothing is sent.

**Why this priority**: This is constitution IV, and the basis of every business flow. Without it, a failure can leave data saved but no event sent, or an event sent for data that was never saved.

**Independent Test**: Run a handler that saves an entity and returns an event, once successfully and once failing after the change. Check the database and the event's receiver each time.

**Acceptance Scenarios**:

1. **Given** a handler that adds an entity and returns an event, **When** it succeeds, **Then** the entity is persisted and the event is delivered to its subscriber.
2. **Given** the same handler that throws after adding the entity, **When** it runs, **Then** the entity is not persisted and the event is never delivered.
3. **Given** a successful handler, **When** the broker is unavailable at commit time, **Then** the event is kept in durable storage and delivered once the broker is reachable.
4. **Given** a handler that tries to save on its own, **When** the code is reviewed, **Then** it violates the documented rule. Enforcement is by convention and review, and the rule is documented in the quickstart.

---

### User Story 2 - A message is handled once, even if delivered twice (Priority: P1)

If the broker delivers the same message twice, which happens after network or broker hiccups, the receiving service processes it only once.

**Why this priority**: Constitution IV requires idempotent consumption. At-least-once delivery is otherwise guaranteed to cause double effects sooner or later.

**Independent Test**: Deliver the same message twice to a listening service, and count how many times its handler runs.

**Acceptance Scenarios**:

1. **Given** a message delivered twice with the same message id, **When** both deliveries arrive, **Then** the handler runs exactly once.
2. **Given** two different messages with identical content, **When** both arrive, **Then** both are handled. Deduplication is by message id, not by content.

---

### User Story 3 - Events reach exactly the services that subscribe (Priority: P1)

A service publishes an integration event under its stable identity (for example `test.thing-happened.v1`). Every service that subscribes to that identity receives one copy on its own queue, and services that don't subscribe receive nothing. Within a subscribing service, the copy is handled by one instance.

**Why this priority**: This is explicit routing (decision B10) and the contract between services.

**Independent Test**: Two independent subscriber hosts and one non-subscriber host on a real broker. The publisher sends one event.

**Acceptance Scenarios**:

1. **Given** subscribers S1 and S2 to identity `X` and a host S3 that does not subscribe, **When** `X` is published once, **Then** S1 and S2 each handle it once, and S3 handles nothing.
2. **Given** an event type with an explicit identity, **When** it is published, **Then** the broker exchange and the message type header use that identity, not the C# type name.
3. **Given** a subscriber that starts before the publisher has ever published, **When** the publisher publishes, **Then** the subscriber still receives the event, because the exchanges and bindings are provisioned by both sides.

---

### User Story 4 - Event handlers run in the right tenant, knowing who started it (Priority: P1)

When an HTTP request for tenant `acme`, made by user `u1`, causes an event, the handler for that event in any service runs with tenant `acme`. It sees the initiating user `u1` and a correlation id shared across the whole chain, so tenant-filtered data access just works.

**Why this priority**: Constitution V (the tenant comes from message headers for messages) and the audit trail.

**Independent Test**: An HTTP request triggers a command that publishes an event. The event handler reports the tenant, user and correlation id it saw, and reads tenant-filtered data.

**Acceptance Scenarios**:

1. **Given** an HTTP request by `u1` for `acme`, **When** its command publishes an event, **Then** the event handler sees tenant `acme`, initiating user `u1`, and the same correlation id as the originating request's message chain.
2. **Given** an event handler for an event of `acme`, **When** it queries tenant-owned data, **Then** only `acme` rows are visible (feature 003's filter is active).
3. **Given** an event that itself publishes a follow-up event, **When** the follow-up is handled, **Then** the tenant, user and correlation id are unchanged.
4. **Given** a message without a tenant (a platform-level event), **When** it is handled, **Then** the handler sees no tenant, and tenant-owned queries return nothing (fail closed).
5. **Given** an event handler base, **When** a developer writes an event handler, **Then** it has access to the tenant, initiating user, correlation id, clock and database, and has **no** access to permission checks.

---

### User Story 5 - Command handlers and HTTP mapping work as designed (Priority: P2)

The design decisions from feature 004 and decision B17 work end to end:
- a handler derived from the command base class is discovered automatically
- an HTTP route mapped directly to a command returns the handler's response body and also publishes the events the handler returned

**Why this priority**: These close the open verification points (B20). It is P2 because US1–US4 already prove the core.

**Independent Test**: A test application maps a POST route to a command handled by a `CommandHandler<TDbContext>`-derived handler that returns a response plus an event.

**Acceptance Scenarios**:

1. **Given** a handler class derived from the command base class, with no registration code, **When** the host starts, **Then** its handler is discovered and runs.
2. **Given** a POST route mapped to a command, **When** it is called, **Then** the response body is the handler's response, the entity is saved, and the returned event is delivered.
3. **Given** the same route and a handler that fails a permission check, **When** it is called, **Then** the response is 403 from feature 004, and nothing is saved or delivered.

### Edge Cases

- The broker is down when a service starts. The service starts anyway, and durable outgoing messages wait in storage.
- A handler for a message type with no subscribers at all. Publishing succeeds, and nothing is delivered. This is not an error.
- The message storage schema does not exist yet. It is created automatically on startup.
- Retrying database execution strategies are incompatible with the per-handler transaction. Wolverine detects this at startup, and the quickstart documents not enabling them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A new shared messaging package MUST provide one setup call per service. Given the service name, the database connection and the broker connection, it configures:
  - durable message storage in the service's own database (its own schema)
  - a transaction per handler covering entity changes and outgoing messages
  - durable inbox on every listener and durable outbox on every sender
  - broker connectivity with automatic provisioning
- **FR-002**: A failing handler MUST leave no persisted changes and no delivered messages. A successful one MUST commit changes and messages together, and deliver after the commit.
- **FR-003**: Incoming messages MUST be deduplicated by message id.
- **FR-004**: The package MUST provide explicit routing helpers:
  - publish an event type to an exchange named by its message identity
  - subscribe the service's own queue to an event identity
  - listen on the service's own queue

  The exchanges and bindings MUST be provisioned by both publishers and subscribers.
- **FR-005**: Integration events MUST carry an explicit, versioned message identity (for example `order.order-placed.v1`), used as the message type on the wire.
- **FR-006**: Every outgoing message MUST carry:
  - the tenant, taken from the incoming message, or, for messages that start in an HTTP request, from the current user
  - the initiating user id, under the same two rules
  - the correlation id of the chain
- **FR-007**: Before any handler runs for an incoming message, the tenant context MUST be set from the message's tenant, if present.
- **FR-008**: The core package MUST define an event-handling context (tenant, initiating user, correlation id, clock) and an event-handler base type that exposes it plus a typed database context, and no permission manager. The name MUST NOT collide with the messaging library's own context type.
- **FR-009**: The feature MUST verify, with automated tests:
  - discovery of handlers derived from the command-handler base
  - an HTTP route mapped to a command that returns the response and cascades events
  - header propagation across two hops
- **FR-010**: All behaviour MUST be tested against real PostgreSQL and RabbitMQ containers, using feature 005's fixtures, with nothing left behind.
- **FR-011**: The package MUST be packable under `building-blocks/v`, with a CI consumer check like the other packages.

### Key Entities

- **Integration event**: an immutable message with a stable, versioned identity, owned by its publishing service.
- **Envelope metadata**: tenant, initiating user, correlation id and message id carried with every message.
- **Service queue**: the single durable inbound queue of a service, bound to the identities it subscribes to.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested failure cases, zero rows persist and zero messages are delivered.
- **SC-002**: A message delivered twice is handled exactly once in 100% of test runs.
- **SC-003**: Each subscribing service receives exactly one copy of each published event, and non-subscribers receive zero.
- **SC-004**: Tenant, initiating user and correlation id are identical at every hop of a two-hop chain.
- **SC-005**: A service enables messaging with one setup call, plus one line per published and per subscribed event.

## Assumptions

- Wolverine 6.65.1 (MIT), with PostgreSQL message storage, the EF Core integration and the RabbitMQ transport.
- Fanout exchanges per event identity, and one durable queue per service. The per-instance broadcast queues needed for feature 007 are out of scope here.
- Validation (FluentValidation) is feature 008. Sagas come with the order flow in Phase 2.
- Tests may use the single-node durability mode for speed. Services use the default clustered mode.
