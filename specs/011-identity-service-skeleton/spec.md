# Feature Specification: Identity Service Skeleton

**Feature Branch**: `011-identity-service-skeleton`

**Created**: 2026-11-24

**Status**: Draft

**Input**: User description: "011 identity-service-skeleton. The first service, Identity, as a runnable and deployable skeleton without OpenIddict (that comes in 012). It covers: the service folder with its own solution, Dockerfile, compose file and CI; its own database with an outbox and inbox, migrations and its own permission store; Wolverine code generation (Dynamic in dev and tests, Static and committed for production, plus a CI guard and a Static smoke start); shared authentication in BuildingBlocks (JWT validation, a fallback policy requiring a tenant, an allow-list, platform-only routes); a reusable no-roles 403 route test; a first endpoint `GET /roles` that requires `identity.roles.view`; and constitution amendment v1.3.0 covering platform-only routes."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run the Identity service in the dev stack (Priority: P1)

A developer runs the dev stack and gets a healthy Identity service with its own database, migrated to the current schema. Health endpoints answer without a token.

**Why this priority**: Nothing in 012–014 can be built without a running first service. This story also proves the service-per-folder build and deploy model from the constitution.

**Independent Test**: On a clean machine, start the `infra` and `apps` profiles and confirm that the Identity database and API both report healthy, and that the readiness endpoint answers 200 without credentials.

**Acceptance Scenarios**:

1. **Given** a clean dev machine, **When** the developer starts the stack with the `infra` and `apps` profiles, **Then** the Identity database and the Identity API both become healthy, and the schema is migrated on startup.
2. **Given** the stack is up, **When** anyone calls the liveness or readiness endpoint without a token, **Then** it answers 200.
3. **Given** the Identity database becomes unreachable while the service runs, **When** the readiness endpoint is called, **Then** it reports unhealthy, while liveness still answers 200.
4. **Given** only the `infra` profile is started, **When** the developer looks at the stack, **Then** the Identity database runs and the API does not.

---

### User Story 2 - Every route is protected by default (Priority: P1)

Every Identity route rejects callers without a valid token, or with a token that doesn't describe a valid caller (401). A tenant route rejects platform users, and a platform route rejects tenant users (403). A route can be reachable without a token only through an explicit, reviewed allow-list.

**Why this priority**: This is the security baseline every later service inherits from BuildingBlocks. It has to be right before any real endpoints exist.

**Independent Test**: Call `GET /roles` with no token, an invalid token, a valid platform token and a valid tenant token without roles, and check that the responses are 401, 401, 403 and 403.

**Acceptance Scenarios**:

1. **Given** no token, an expired token, a token signed by an unknown key or a token for the wrong audience, **When** any non-allow-listed route is called, **Then** the response is 401.
2. **Given** a signed token that is malformed as an identity (a non-platform user without a tenant, an unknown `user_type`, or a platform user with a tenant), **When** any non-allow-listed route is called, **Then** the response is 401 `invalid-identity`.
3. **Given** a valid platform token (`user_type=platform`, no tenant), **When** a tenant route is called, **Then** the response is 403. **When** a platform route is called by a tenant user, **Then** the response is also 403.
4. **Given** a new route is added with no marker, **When** the service runs, **Then** that route requires an authenticated user with a tenant, with no extra configuration.

---

### User Story 3 - Every route has a permission check, proven automatically (Priority: P1)

A test discovers every route of the service and calls each one as an authenticated user with no roles: a tenant user for tenant routes, a platform user for platform routes. It expects 403 from every route except the allow-listed ones. Any service can reuse the same test.

**Why this priority**: The constitution makes this a merge gate. Without it, a route that forgot its permission check would ship silently.

**Independent Test**: Run the route test on Identity: it passes. Add a scratch route without a permission check: the test fails and names that route. Remove the scratch route.

**Acceptance Scenarios**:

1. **Given** the Identity service, **When** the no-roles route test runs, **Then** every non-allow-listed route answers 403, and the test lists the routes it checked.
2. **Given** a route that returns data without checking a permission, **When** the test runs, **Then** it fails and names the route and the status it got.
3. **Given** a route that is allow-listed, **When** the test runs, **Then** that route is skipped and reported as skipped.
4. **Given** another service references the shared testing package, **When** it adds the test with a few lines of setup, **Then** the same checks run against that service.

---

### User Story 4 - Staff can see Identity's role catalogue (Priority: P2)

A tenant user who holds a role granting `identity.roles.view` calls `GET /roles` and receives Identity's fixed role catalogue: each role's name and the permissions it grants, plus action flags. A user without the permission gets 403 problem details that name the missing permission.

**Why this priority**: It's the first real endpoint, and it exercises the whole request path end to end: authentication, tenant, permission check, handler and database. Identity's own role management in later features builds on it.

**Independent Test**: In an integration test, assign a role that grants `identity.roles.view` to a tenant user in the Identity database, call `GET /roles` with that user's token, and compare the response with the catalogue defined in code.

**Acceptance Scenarios**:

1. **Given** a tenant user whose assigned role grants `identity.roles.view`, **When** they call `GET /roles`, **Then** the response is 200 with every role from Identity's catalogue, each listing its permissions.
2. **Given** a tenant user with no roles, **When** they call `GET /roles`, **Then** the response is 403 problem details that list `identity.roles.view` as missing.
3. **Given** the catalogue is defined in code, **When** the response is returned, **Then** it includes screen-level action flags (`canAssign`) combining the caller's permissions, as the constitution requires.

---

### User Story 5 - The production image runs pre-generated code, guarded by CI (Priority: P2)

In development and tests, the service generates its message-handling code at runtime. The production image runs only pre-generated code that is committed to the repo. CI regenerates the code and fails if it differs from what was committed, and it starts the production image to prove it runs in pre-generated mode.

**Why this priority**: This is a locked decision. It makes production start-up deterministic and fast, and it stops stale generated code from shipping.

**Independent Test**: Change a handler signature without regenerating, and confirm that CI's guard fails. Regenerate and commit, and confirm the guard passes and the image starts healthy in pre-generated mode.

**Acceptance Scenarios**:

1. **Given** the committed generated code matches the handlers, **When** CI runs the codegen guard, **Then** it passes.
2. **Given** a handler was changed without regenerating, **When** CI runs the guard, **Then** it fails and shows which generated files differ.
3. **Given** the production image is built, **When** CI starts it with its database, **Then** it reports healthy, and it never compiles code at runtime.
4. **Given** a development or test run, **When** the service starts, **Then** it generates code at runtime, and no committed file changes.

---

### User Story 6 - The service builds and ships on its own (Priority: P2)

The Identity service builds, tests and packages from its own folder only. Shared code comes in as versioned packages, its CI runs only when its own folder changes, and its image builds with only its own folder as the build context.

**Why this priority**: Principle I of the constitution. This first service sets the template every later service copies.

**Independent Test**: Change a file outside `src/services/identity/` (not BuildingBlocks), and confirm the Identity workflow does not run. Build the Identity image, and confirm the build context contains only the service folder.

**Acceptance Scenarios**:

1. **Given** a change only under `src/services/identity/`, **When** it is pushed, **Then** the Identity workflow runs, and the BuildingBlocks workflow does not.
2. **Given** the Identity image is built, **When** the build context is inspected, **Then** it contains only the service folder, and the BuildingBlocks packages arrive through the package feed.
3. **Given** BuildingBlocks gets the new authentication and route-test features, **When** they are released as a new package version, **Then** Identity references that version, and no project reference points outside its folder.

---

### User Story 7 - The constitution covers platform-only routes (Priority: P3)

The constitution states that routes require an authenticated user with a tenant by default, that platform-only routes are an explicit, reviewed category open only to platform users without a tenant, and that the no-roles route test covers both kinds.

**Why this priority**: The current wording ("every route requires an authenticated user with a tenant") would lock the platform admin out of everything. The rule must match the design before platform routes arrive in 013.

**Independent Test**: Read principle VI and the footer of the amended constitution.

**Acceptance Scenarios**:

1. **Given** the amended constitution, **When** a reviewer reads the authorization principle, **Then** it describes the default (tenant required), the allow-list, and the platform-only category accepting only `user_type=platform` without a tenant.
2. **Given** the amended constitution, **When** a reviewer reads the footer, **Then** the version is 1.3.0 and the last-amended date is updated.

---

### Edge Cases

- **A token claims `user_type=platform` and also carries a tenant.** It is rejected with 401 `invalid-identity` everywhere, because platform tokens must not carry a tenant.
- **A token has a tenant but an unknown `user_type`.** It is rejected with 401 `invalid-identity`.
- **The signing keys can't be fetched at start-up** (the authority is unreachable). The service still starts and stays live. Requests that need a token answer 401 until the keys can be fetched, and readiness does not depend on the authority.
- **The signing keys rotate.** A token signed with a new key is accepted once the cache refreshes, without a restart.
- **A route is marked both allow-listed and platform-only.** Startup fails with a clear configuration error.
- **The database is unreachable at start-up.** The service fails fast, because the messaging store needs the database to start, and the container restarts until the database is reachable. Compose starts the API only after the database is healthy. *(Changed during implementation: Wolverine's message storage cannot start without its database.)*
- **The database goes away while the service runs.** Readiness reports unhealthy and liveness stays 200, until the database is back.
- **The same migration is started by two instances at once.** Only one applies it, and the other waits or does nothing.
- **Dev and Testcontainers run in parallel.** The Identity tests never use the dev database or the dev broker.

## Requirements *(mandatory)*

### Functional Requirements

**Service layout and runtime**

- **FR-001**: The repository MUST contain `src/services/identity/` with its own solution, API project, Contracts project (packable, with no messages yet), test project, central package versions, Dockerfile and compose file, as the constitution's repository layout requires.
- **FR-002**: The service MUST build, test and produce its image from its own folder only. No project reference may point outside it, and the BuildingBlocks code MUST come from versioned packages.
- **FR-003**: The Dockerfile MUST use only its own folder as the build context. It receives the package feed through a named build context, and takes its base images only from build arguments supplied from `deploy/versions.env`, with no default values.
- **FR-004**: The service's compose file MUST define the Identity API in profile `apps` and its Postgres database in profile `infra`. The database publishes `127.0.0.1:${IDENTITY_DB_PORT}`, keeps its data in a named volume, and has a healthcheck. The API has a healthcheck and waits for a healthy database and a healthy broker. The root `deploy/compose.yaml` MUST include the file with one entry.
- **FR-005**: The API MUST NOT publish a host port, except an optional loopback-only dev port documented as temporary until the gateway (feature 014) becomes the only entry point.
- **FR-006**: The service MUST expose a liveness endpoint, which checks only the process, and a readiness endpoint, which checks the database. Both are on the allow-list.
- **FR-007**: The service MUST apply its database migrations on start-up, safely when several instances start at once. Readiness turns healthy only after the migrations are applied.

**Persistence and messaging**

- **FR-008**: The service MUST own one database holding its tables, the transactional outbox and inbox, and its role assignments.
- **FR-009**: Message handling MUST use the BuildingBlocks messaging setup: transactions, outbox and inbox, and the tenant from message headers. Failed messages MUST NOT be logged with their full content.

**Authorization**

- **FR-010**: Identity MUST define its roles and permissions in code. It defines at least the permission `identity.roles.view` and a role that grants it. Role assignments are stored in Identity's database and resolved through the BuildingBlocks permission manager.
- **FR-011**: BuildingBlocks MUST provide a reusable authentication setup for services. It validates signed access tokens against a configured authority and audience, caching the authority's signing keys and refreshing them when they rotate. Tokens that are missing, expired, wrongly signed or for the wrong audience get 401.
- **FR-012**: That setup MUST apply a default policy to every route: an authenticated user with a tenant, and a `user_type` other than `platform`. Routes MUST be able to opt out only by an explicit allow-list marker, or opt into the platform-only category by an explicit platform marker.
- **FR-013**: A platform-only route MUST accept only users with `user_type=platform` and no tenant. A well-formed tenant user gets 403 there. A token that is malformed as an identity (a non-platform user without a tenant, an unknown `user_type`, or a platform user with a tenant) MUST be rejected everywhere with 401 `invalid-identity`.
- **FR-014**: A route that carries both the allow-list and the platform markers MUST cause a start-up failure.

**Route test**

- **FR-015**: The BuildingBlocks testing package MUST provide a reusable no-roles route test. It discovers every endpoint of a service under test, calls each one as an authenticated user with no roles (a tenant user for tenant routes, a platform user for platform routes) and expects 403. It skips allow-listed routes, and reports the routes it checked, the routes it skipped and every failure with its route and status.
- **FR-016**: The route test MUST fill route parameters with placeholder values, so parameterised routes are called too, and MUST cover every HTTP method of each route.
- **FR-017**: Identity's test suite MUST run the route test as a merge gate.

**First endpoint**

- **FR-018**: `GET /roles` MUST return Identity's role catalogue, with each role's name and permissions, to callers holding `identity.roles.view`. Others get 403 problem details that list the missing permission.
- **FR-019**: The response MUST include the screen-level action flag `canAssign`, which is true only when the caller may assign roles. Role assignment itself is not in this feature, so the flag is false for every caller until that feature exists.
- **FR-020**: The HTTP layer MUST only map the route to a handler. The logic MUST live in the handler, which goes through the BuildingBlocks command-handler base class and the permission manager.

**Code generation**

- **FR-021**: In development and tests, the service MUST generate its message-handling code at runtime. The runtime compiler is referenced only in the Debug configuration.
- **FR-022**: Release builds and the production image MUST use only pre-generated, committed code, and MUST NOT compile code at runtime.
- **FR-023**: CI MUST regenerate the code and fail if the result differs from the committed files.
- **FR-024**: CI MUST build the production image, start it with a database, and require it to become healthy in pre-generated mode.

**CI**

- **FR-025**: A workflow `identity.yml` MUST run only for changes to `src/services/identity/**` and the workflow file itself. It runs the build, unit and integration tests, the no-roles route test, the codegen guard, the pre-generated smoke start, the image-pin check and the test cleanup check.
- **FR-026**: The BuildingBlocks additions (authentication setup and route test) MUST ship through the existing BuildingBlocks workflow, with their own tests and consumer checks, as a new package version that Identity references.

**Constitution**

- **FR-027**: The constitution MUST be amended to v1.3.0 (MINOR). In the authorization principle it MUST state the default (an authenticated user with a tenant), the reviewed allow-list, and the platform-only category (`user_type=platform`, no tenant). It MUST state that the no-roles route test covers both tenant and platform routes. The last-amended date is updated.

### Key Entities

- **Role (catalogue entry)**: a fixed role defined in Identity's code, with a stable name and the set of permissions it grants. It is not stored as a row.
- **Permission**: a stable string such as `identity.roles.view`, defined in code.
- **Role assignment**: a stored link between a user, a tenant and a role in Identity's database. It is the source the permission manager reads.
- **Route category**: each route is tenant (the default), platform-only or allow-listed. The authentication setup and the route test both read this category.
- **Outbox and inbox records**: the messaging infrastructure's tables in Identity's database.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a clean clone with images pulled, the developer has a healthy Identity service and database in under 90 seconds with one command.
- **SC-002**: 100% of Identity's non-allow-listed routes answer 403 to a no-roles user and 401 to a caller without a token, verified on every pull request.
- **SC-003**: A route added without a permission check fails CI on the first run, with the route named in the failure output.
- **SC-004**: The production image starts healthy with zero runtime code compilation, and a stale generated-code change is caught by CI 100% of the time.
- **SC-005**: A change outside the Identity folder and BuildingBlocks triggers 0 Identity CI runs. A change inside it triggers exactly 1.
- **SC-006**: A second service can adopt the shared authentication setup and the route test with no more than about 10 lines of setup each.
- **SC-007**: After any test run, 0 test containers or volumes remain, and the dev stack is untouched.

## Assumptions

- No token issuer exists yet. Tests mint tokens with a test signing key, and the dev configuration points the authority at Identity's future address. In development the API runs without valid tokens until feature 012 adds the issuer, so only health endpoints are usable by hand in dev.
- Identity will switch to validating its own tokens locally (OpenIddict) in 012. The shared setup is what every other service uses. Identity uses it in this feature so it can be tested now.
- The audience value for each service is configuration (for example `identity`), and the token issuer is set up to match it in 012.
- The role catalogue starts with one role (for example "identity administrator") granting `identity.roles.view`. More roles and permissions arrive with tenant and user management in 013.
- Role assignments are seeded only by tests in this feature. Seeding the platform admin and the first tenant admin comes in 012 and 013.
- The BuildingBlocks version bump follows the existing MinVer and local-feed process from feature 001.
- The gateway (014) will be the only published entry point. Until then, a loopback-only dev port for the API is allowed and documented.
