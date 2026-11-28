# Feature Specification: Product Service Skeleton

**Feature Branch**: `024-product-service-skeleton`

**Created**: 2026-11-27

**Status**: Draft

**Input**: User description: "024 product-service-skeleton: the Product service as a runnable, deployable skeleton, following Identity's 011 and reusing the existing BuildingBlocks packages. It covers: src/services/product/ with Product.slnx, Product.Api, Product.Contracts (packable, no messages yet), Product.Tests, central package versions, a Dockerfile built from its own folder only, and compose.yaml (API in profile apps, its own Postgres in profile infra on PRODUCT_DB_PORT with a named volume and healthchecks), included from deploy/compose.yaml; one database with the Wolverine outbox and inbox, migrations applied on start, ready only after they ran; BuildingBlocks JWT validation against Identity with audience product, the tenant default policy, liveness and readiness on the allow-list; a first endpoint GET /roles returning Product's role catalogue from code (permission product.roles.view, an admin and a viewer role) with canAssign false (assignments and local users come in 025, so until then everyone gets 403); an /api/product/** route in the gateway with its route-table test updated; Wolverine codegen Dynamic in dev and tests, Static and committed for production, with the CI guard and a Static smoke start; the no-roles 403 route test; a product.yml CI workflow (paths filter, build, tests, codegen guard, smoke start, image-pin and cleanup checks); the package feed and e2e stack wiring the new service needs. No UI change. Tenant currency, products and events come in 026; local users and the first-admin role grant in 025. Owner decisions for the Product service are in the project memory (five-feature split, core fields, one currency per tenant, fixed SKU unique per tenant)."

## User Scenarios & Testing *(mandatory)*

The users of this feature are the developers and the owner. The Product service holds no business data yet; it must run, deploy and protect itself like Identity does, so the catalog features (025–028) start from a working service.

### User Story 1 - Run the Product service in the dev stack (Priority: P1)

A developer starts the dev stack and the Product service comes up next to Identity, with its own database, and reports itself ready once its database is migrated.

**Why this priority**: Nothing else in the Product roadmap can be built or tried without a running service.

**Independent Test**: Start the stack with the infrastructure and applications profiles: the Product service and its database become healthy, and its readiness turns healthy only after the database schema is in place.

**Acceptance Scenarios**:

1. **Given** the dev stack, **When** it starts with the applications profile, **Then** the Product service and its own database start and report healthy.
2. **Given** a fresh database, **When** the service starts, **Then** it creates its schema before it reports ready.
3. **Given** two instances starting at once on a fresh database, **When** both apply the schema, **Then** both become ready and the schema is applied once.
4. **Given** the database is down, **When** readiness is checked, **Then** it reports not ready, while liveness still reports alive.

---

### User Story 2 - Reach the service only through the gateway, signed in (Priority: P1)

A signed-in backoffice user's requests to `/api/product/...` reach the Product service through the gateway. Requests without a valid sign-in are refused.

**Why this priority**: Every later Product screen depends on this path, and the service must never be open to anonymous callers.

**Independent Test**: Signed in as tenant staff, request `/api/product/roles` through the gateway: the request reaches the service (403, because no one holds Product roles yet). Without a session, the gateway answers 401.

**Acceptance Scenarios**:

1. **Given** a signed-in tenant user, **When** they call a Product route through the gateway, **Then** the call reaches the service with the user's identity.
2. **Given** no session, **When** a Product route is called through the gateway, **Then** the answer is 401 and the service is not reached.
3. **Given** a token for another service's audience, an expired token or a wrongly signed token, **When** it is sent to the service directly, **Then** the answer is 401.
4. **Given** the platform admin (no tenant), **When** they call a tenant route of the Product service, **Then** the answer is 403.

---

### User Story 3 - Product's roles are defined and enforced (Priority: P2)

The service defines its own roles and permissions. `GET /roles` lists them to callers who may view them; everyone else gets 403 with the missing permission. Until role assignment exists (025), nobody holds a Product role.

**Why this priority**: It proves the per-service authorization path end to end and gives the no-roles route test a real route to check.

**Independent Test**: Call `GET /roles` as a tenant user with no Product roles: 403 naming `product.roles.view`. In a test that grants the viewer role directly, the call returns the admin and viewer roles with their permissions and `canAssign` false.

**Acceptance Scenarios**:

1. **Given** a caller holding `product.roles.view`, **When** they call `GET /roles`, **Then** they get every Product role with its name, display name and permissions, and `canAssign` is false.
2. **Given** a caller without it, **When** they call `GET /roles`, **Then** they get 403 listing the missing permission.

---

### User Story 4 - Ship the service safely (Priority: P2)

A change under the Product folder runs its own CI: build, tests, the no-roles route test, the generated-code guard, a smoke start of the production image, the image-pin check and the test cleanup check. Changes elsewhere don't run it.

**Why this priority**: The constitution's merge gates apply to every service from its first feature.

**Independent Test**: Open a pull request that changes only the Product folder: the Product workflow runs every gate and passes; a change only to Identity does not start it.

**Acceptance Scenarios**:

1. **Given** a change under the Product folder, **When** CI runs, **Then** every gate runs and passes.
2. **Given** the committed generated code is out of date, **When** CI runs, **Then** the guard fails.
3. **Given** the production image, **When** it starts with a database in pre-generated mode, **Then** it becomes healthy without compiling code at runtime.

---

### Edge Cases

- **A route added later without a permission check**: the no-roles route test fails CI.
- **Identity's signing keys rotate**: the service picks up the new keys without a restart.
- **The broker is down at start-up**: the service waits for it, as Identity does.
- **The end-to-end stack**: it includes the Product service and its database, and still leaves nothing behind after the run.
- **No UI change**: the backoffice and storefront are unchanged; the gateway only gains the route.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST contain `src/services/product/` with its own solution, API project, Contracts project (packable, with no messages yet), test project, central package versions, Dockerfile and compose file, following the constitution's repository layout.
- **FR-002**: The service MUST build, test and produce its image from its own folder only. No project reference may point outside it, and BuildingBlocks MUST come from the versioned packages already published.
- **FR-003**: The Dockerfile MUST use only its own folder as the build context, receive the package feed through a named build context, and take its base images only from build arguments supplied from the pinned versions file, with no default values.
- **FR-004**: The service's compose file MUST define the API in the applications profile and its own database in the infrastructure profile. The database publishes only on loopback at `PRODUCT_DB_PORT`, keeps its data in a named volume and has a healthcheck. The API has a healthcheck, waits for a healthy database and broker, and publishes no host port. The root compose file MUST include it with one entry.
- **FR-005**: The service MUST expose liveness (process only) and readiness (database, after migrations) endpoints on the allow-list.
- **FR-006**: The service MUST apply its migrations on start, safely when several instances start at once.
- **FR-007**: The service MUST own one database holding its tables and the transactional outbox and inbox, set up through the BuildingBlocks messaging setup.
- **FR-008**: The service MUST validate access tokens through the BuildingBlocks authentication setup, against Identity as the authority and with audience `product`, and apply the default tenant policy to every route except the allow-list.
- **FR-009**: The service MUST define its roles and permissions in code: at least the permission `product.roles.view`, a viewer role that grants it and an admin role that grants every Product permission, each with a display name. Role assignments are stored in the service's own database and resolved through the BuildingBlocks permission manager.
- **FR-010**: `GET /roles` MUST return Product's role catalogue with each role's name, display name and permissions to callers holding `product.roles.view`, with the screen-level flag `canAssign` false for every caller until role assignment exists. Others get 403 listing the missing permission.
- **FR-011**: The HTTP layer MUST only map routes to handlers; the logic lives in handlers built on the BuildingBlocks base classes.
- **FR-012**: The service's test suite MUST run the BuildingBlocks no-roles route test as a merge gate.
- **FR-013**: In development and tests the service MUST generate its message-handling code at runtime; release builds and the image MUST use only committed, pre-generated code, and CI MUST fail when that code is out of date.
- **FR-014**: CI MUST build the production image, start it with a database in pre-generated mode, and require it to become healthy.
- **FR-015**: A workflow for the Product service MUST run only for changes to its folder and the workflow file, and run the build, tests, the no-roles route test, the generated-code guard, the smoke start, the image-pin check and the test cleanup check.
- **FR-016**: The gateway MUST forward `/api/product/**` to the Product service with the session's access token, like `/api/identity/**`, and its route-table test MUST cover the new route.
- **FR-017**: The end-to-end stack MUST include the Product service and its database, and its cleanup MUST still leave nothing behind.
- **FR-018**: The existing dev-stack workflow, which validates the compose configuration and starts the stack, MUST also run for changes to the Product folder and restore the Product service's package feed, as it does for Identity.

### Key Entities

- **Product permission**: a named permission owned by the Product service, for example `product.roles.view`.
- **Product role**: a named set of Product permissions with a display name (admin, viewer).
- **Role assignment**: a user's Product role within a tenant, stored by the Product service (empty until 025).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a clean checkout with a warm build cache, starting the dev stack brings the Product service to ready within 60 seconds, with no manual step.
- **SC-002**: 100% of Product routes that are not on the allow-list answer 403 to a signed-in user with no Product roles, and 401 without a valid token.
- **SC-003**: Every merge gate the constitution lists runs for the Product service and passes.
- **SC-004**: A change outside the Product folder never starts the Product workflow, and a Product change never starts another service's own workflow (Identity, gateway, the SPAs). The stack-wide dev-stack and end-to-end workflows run for it by design.
- **SC-005**: The existing Identity, gateway, backoffice, storefront and end-to-end checks still pass.

## Assumptions

- Identity already issues tokens with the `product` audience (the `product` scope exists), and the gateway already requests that scope.
- `PRODUCT_DB_PORT` (5402) is already reserved in the dev settings.
- The BuildingBlocks packages already provide authentication, permissions, messaging, validation, lookups, testing and the no-roles route test; no BuildingBlocks change is expected. If one is needed, it ships through the BuildingBlocks workflow as a new version.
- Local users, the first-admin role grant and role assignment endpoints come in 025; tenant currency, products and events in 026; the backoffice Products page in 027; the storefront catalog in 028.
- No UI change; Product roles do not yet appear in the backoffice.
