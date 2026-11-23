# Feature Specification: BuildingBlocks Core

**Feature Branch**: `002-building-blocks-core`

**Created**: 2026-11-23

**Status**: Draft

**Input**: User description: "Feature 002 of Phase 1. The first real content of the shared BuildingBlocks packages: who is calling (current user: user id, tenant, user type, read from the access token), which tenant a request belongs to (tenant context), and one central translation of domain errors into standard HTTP error responses. A DomainException carries an error code and a kind: Conflict → 409, Rule → 422. NotFoundException → 404. EF Core tenancy (query filter, TenantId stamping) is a separate later feature."

## User Scenarios & Testing *(mandatory)*

The users of this feature are the developers of every service. Each service consumes these packages to learn who is calling and to return consistent errors.

### User Story 1 - Know who is calling (Priority: P1)

A handler in any service asks for the current user and gets the user id, the tenant and the user type (staff, customer or platform). It never reads token claims by hand.

**Why this priority**: Every later piece (permissions, ownership checks, auditing, tenancy) depends on a reliable, typed current user.

**Independent Test**: Build a request identity from a set of claims and read the current user. The values match the claims, and missing or malformed claims behave as defined below.

**Acceptance Scenarios**:

1. **Given** an authenticated request with `sub`, `tenant_id` and `user_type=staff`, **When** a handler reads the current user, **Then** it gets that user id, tenant and user type `Staff`.
2. **Given** an authenticated platform admin with `user_type=platform` and no `tenant_id`, **When** the current user is read, **Then** the tenant is absent and the user type is `Platform`.
3. **Given** an unauthenticated request, **When** the current user is read, **Then** it reports "not authenticated", and reading the user id fails with a clear error instead of returning a default value.
4. **Given** an authenticated request whose `sub` is not a valid identifier, or whose `user_type` is unknown, **When** the request is processed, **Then** it is rejected as unauthenticated (401) and never reaches the handler.
5. **Given** a staff or customer user without `tenant_id`, **When** the request is processed, **Then** it is rejected (401), because only platform users may lack a tenant.

---

### User Story 2 - Know which tenant a request belongs to (Priority: P1)

Every request and every unit of work has a tenant context that later features use for data isolation. For HTTP requests it comes from the current user. Background and message processing (feature 005) set it explicitly, once.

**Why this priority**: Multi-tenancy from day one (constitution V). The EF tenancy feature that follows builds directly on this context.

**Independent Test**: After an authenticated request is processed, the tenant context holds the user's tenant. Setting the tenant a second time to a different value is refused.

**Acceptance Scenarios**:

1. **Given** an authenticated staff request for tenant `acme`, **When** the handler reads the tenant context, **Then** it holds `acme`.
2. **Given** a platform request without a tenant, **When** the tenant context is read, **Then** it reports "no tenant".
3. **Given** a tenant context already set to `acme`, **When** code tries to set it to `globex`, **Then** the attempt fails with a clear error and the context stays `acme`.
4. **Given** a tenant context already set to `acme`, **When** code sets it to `acme` again, **Then** that succeeds and nothing changes.
5. **Given** two concurrent requests for different tenants, **When** both are processed, **Then** each sees only its own tenant.

---

### User Story 3 - Consistent error responses for domain errors (Priority: P1)

When domain code rejects an operation, the service returns a standard machine-readable error response with a stable error code. The UI and other clients can then react to the code rather than parse messages.

**Why this priority**: Handlers must not translate errors themselves (constitution III). One central mapping keeps every service consistent from its first endpoint.

**Independent Test**: An endpoint that raises each error type returns the expected status and body.

**Acceptance Scenarios**:

1. **Given** domain code raises a conflict error with code `order.cancel.already-shipped`, **When** the response is produced, **Then** the status is 409 and the body has a title, the status, `code: "order.cancel.already-shipped"`, the message as `detail`, and a trace id.
2. **Given** domain code raises a rule error with code `inventory.transfer.insufficient-stock`, **When** the response is produced, **Then** the status is 422 with the same body shape.
3. **Given** code raises a not-found error for a resource, **When** the response is produced, **Then** the status is 404 with the same body shape and code `not-found`, and nothing reveals whether the resource exists for another tenant.
4. **Given** any other unexpected exception, **When** the response is produced, **Then** the status is 500, and the body has a generic title, a trace id and no exception message or stack trace.
5. **Given** any error response, **When** its trace id is looked up in the service logs, **Then** the full error is logged under that trace id. 500 responses are logged as errors, and 4xx domain errors as warnings or lower.

---

### User Story 4 - One-line adoption by a service (Priority: P2)

A service enables all of the above with one registration call and one pipeline call, and gets the packages from the local feed by pinned version.

**Why this priority**: Adoption must be trivial and identical in every service. It is P2 because the behaviour itself is covered by US1–US3.

**Independent Test**: A minimal web app, built only from the packaged BuildingBlocks, makes the two calls. All of the behaviour of US1–US3 then works without other setup.

**Acceptance Scenarios**:

1. **Given** a web app that references the packages by version, **When** it makes the registration and pipeline calls, **Then** current user, tenant context and error mapping all work.
2. **Given** a non-web consumer, such as a domain library, **When** it references only the core package, **Then** it gets the abstractions and error types without any web framework dependency.

### Edge Cases

- A token carries `tenant_id` and `user_type=platform`. The tenant is honoured, so a platform user may act inside a tenant when a token says so.
- `user_type` differs only in case (`Staff`). It is accepted case-insensitively.
- A domain error with an empty or missing code is a programming error. Constructing it fails immediately, rather than producing a response without a code.
- An exception is raised after the response has started streaming. It cannot be converted, so it is logged and the connection is aborted, which is standard behaviour.
- An error happens before authentication runs. The error mapping still applies.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The core package MUST define the current-user abstraction:
  - `IsAuthenticated`
  - `UserId`, which fails when the caller is not authenticated
  - `TenantId`, which is optional
  - `UserType` (Staff, Customer, Platform)
- **FR-002**: The core package MUST define the claim names it reads as named constants: `sub`, `tenant_id` and `user_type`.
- **FR-003**: The core package MUST define a tenant context that can be read (`TenantId`, `HasTenant`) and set only once per scope (set-once semantics, idempotent for the same value).
- **FR-004**: The core package MUST define the domain error types:
  - a domain error with a required stable `Code`, a `Kind` (Conflict or Rule) and a message
  - a not-found error with code `not-found`
- **FR-005**: The core package MUST NOT depend on any web framework.
- **FR-006**: The web package MUST provide a current-user implementation that reads the authenticated request identity. Malformed or inconsistent identity (an invalid `sub`, an unknown `user_type`, an empty `tenant_id`, or a staff or customer user without a tenant) is rejected with 401 before any handler runs.
- **FR-007**: The web package MUST populate the tenant context from the current user for every authenticated request, before any handler runs.
- **FR-008**: The web package MUST map exceptions to RFC 9457 problem details:
  - domain Conflict → 409
  - domain Rule → 422
  - not-found → 404
  - anything else → 500

  Bodies carry `type`, `title`, `status`, `detail` and `traceId`, plus `code` for every domain and not-found error. 500 responses never include exception messages or stack traces.
- **FR-009**: Every mapped exception MUST be logged with the trace id. 500 responses are logged at error level, and domain and not-found errors at a lower level.
- **FR-010**: The web package MUST offer a single service-registration call and a single pipeline call that enable FR-006 to FR-009.
- **FR-011**: Both packages MUST be produced by the existing pack pipeline under the `building-blocks/v` tag prefix. Their unit and in-process tests MUST run in CI on every pull request touching `src/building-blocks/**`, without Docker.
- **FR-012**: The feature MUST NOT include authentication setup (token validation), permissions, persistence or messaging. Those belong to later features. Tests simulate an authenticated identity.

### Key Entities

- **Current user**: the identity of the caller (user id, optional tenant, user type).
- **Tenant context**: the tenant a unit of work runs for. It is set once.
- **Domain error**: a rejected operation, with a stable code, a kind (Conflict or Rule) and a message.
- **Error response**: the standard problem body sent to clients.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every acceptance scenario in US1–US4 is covered by an automated test that passes in CI without Docker.
- **SC-002**: A service adopts all the behaviour with exactly 2 lines of setup code.
- **SC-003**: 100% of error responses from the mapping carry a trace id, and 100% of domain and not-found responses carry a code.
- **SC-004**: Zero 500 responses expose exception text or stack traces, as verified by tests with distinctive exception messages.
- **SC-005**: The core package has zero web-framework dependencies, as verified from the package's dependency list.

## Assumptions

- Phase 1 roadmap after this change:
  - 002 building-blocks-core
  - 003 building-blocks-ef-tenancy
  - 004 building-blocks-permissions
  - 005 building-blocks-messaging
  - 006 dev-compose-infra
  - 007 identity-authentication
  - 008 identity-tenants-users
  - 009 api-gateway
- Two packages: `BuildingBlocks.Core` (no web dependency, so domain libraries can use it) and `BuildingBlocks.AspNetCore` (HTTP integration).
- Validation errors (400 with field errors) are out of scope until a validation approach is chosen in a later feature.
- English titles. `detail` uses the exception message for domain errors. Localization is out of scope.
- The trace id is the current distributed-trace identifier when one exists, and the request identifier otherwise.
