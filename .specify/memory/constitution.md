# Inventory & Order Fulfillment Constitution

## Core Principles

### I. Microservices, Independently Deployable (NON-NEGOTIABLE)

The system is a microservices architecture. Every service MUST build, test, package and deploy
from its own folder (`src/services/<name>/`) without any other service being built, running or
redeployed.

- A service MUST own its data in its own PostgreSQL container. No service reads or writes
  another service's database.
- A service's Docker build context MUST be only its own folder. Shared packages come in through a
  BuildKit named context for the local package feed.
- No project reference may point outside the service's own folder.
- Synchronous call chains between services are forbidden. Collaboration happens through
  asynchronous integration events. REST is for client queries through the gateway. A rare
  service-to-service HTTP call MUST use the client-credentials flow and MUST NOT be part of a chain.
- Each service has its own CI workflow, triggered only by changes to its own folder.

Rationale: if services must change or deploy together, the result is a distributed monolith,
which defeats the purpose of the project.

### II. Shared Code Only via Versioned Packages

The only shared code is:

- **BuildingBlocks**: infrastructure only (tenancy, authentication, permission plumbing, telemetry,
  Wolverine setup, problem details, base handlers). It MUST NOT contain domain types or business
  rules.
- **`<Service>.Contracts`**: one library per publishing service, containing only integration message
  records with an explicit Wolverine message identity (e.g. `order.order-placed.v1`). The
  publishing service owns it. Consumers reference only the contracts packages they need.

Both are consumed only as NuGet packages: a local folder feed at `artifacts/packages/`, MinVer
versions from per-package tag prefixes, and versions pinned per service in that service's
`Directory.Packages.props`. Contract changes MUST be additive only. A breaking change MUST be
introduced as a new versioned message (`...v2`) and published alongside the old one until all
consumers have migrated.

Rationale: pinned versions let each service upgrade its dependencies on its own schedule.

### III. Handlers Hold All Logic; the Domain Holds All Rules

- All application logic MUST live in Wolverine handlers. HTTP routes only map to handlers (e.g.
  `MapPostToWolverine<TCommand, TResult>`), and endpoint classes contain no logic.
- Business rules and invariants MUST live in the domain model (aggregates and domain services),
  which has no dependency on HTTP, persistence or permissions. Domain violations raise
  `DomainException`, which is mapped centrally to 409/422 problem details.
- HTTP-triggered commands use the `CommandHandler<TDbContext>` base class (user, permissions,
  database). Message-triggered work uses `EventHandler<TDbContext>` (tenant, correlation and
  initiating user from message headers, with no permission access). Logic shared by both lives in
  the domain.
- Code is organized in vertical slices: `Features/<UseCase>/` holds the command, handler and
  validator together.

### IV. Transactional Consistency and Reliable Messaging (NON-NEGOTIABLE)

- Every handler runs in one database transaction through Wolverine's automatic transactions and
  EF Core integration. Entity changes and outgoing messages (durable outbox) commit together, and
  messages are dispatched only after the commit. Any exception rolls back everything, so nothing
  is saved and nothing is sent.
- Handlers MUST NOT call `SaveChangesAsync` themselves.
- Handlers MUST NOT perform side effects outside their own database (email, SMS, external APIs,
  files). Such effects MUST be triggered by publishing an event for the owning service.
- Every message handler MUST be idempotent. The durable inbox deduplicates messages, and handlers
  MUST tolerate redelivery anyway.
- Cross-service consistency is handled by the orchestrated `OrderFulfillmentSaga` and its
  compensating actions, never by distributed transactions.
- HTTP idempotency keys are stored in PostgreSQL in the same transaction as the operation.

### V. Multi-Tenancy by Default (NON-NEGOTIABLE)

- Every tenant-owned entity in every service carries `TenantId`. An EF Core global query filter
  restricts every query to the current tenant, and a SaveChanges interceptor stamps `TenantId`.
  Code never sets it by hand.
- The tenant is resolved from the JWT `tenant_id` claim for HTTP requests, and from the `tenant-id`
  message header for messages.
- Access to another tenant's resource MUST behave as not found (404).
- A user belongs to exactly one tenant. The platform admin has no tenant.

### VI. Decentralized Authorization with Explicit Checks

- The Identity Service (OpenIddict + ASP.NET Identity) does authentication only. Access tokens
  carry `sub`, `tenant_id` and `user_type` (staff, customer, platform) and MUST NOT carry roles or
  permissions.
- Each service owns its authorization: fixed permissions and fixed roles defined in its own code,
  and user-to-role assignments per tenant in its own database. All customers receive a fixed
  default permission set defined in code, with no per-customer assignments. Ownership rules (e.g.
  a customer sees only their own orders) are enforced in handlers.
- Permission checks MUST be explicit calls to the injected `IPermissionManager` (`EnsureAsync`,
  `EnsureAllAsync`, `HasAsync`, `HasAnyAsync`, `HasAllAsync`, `GetGrantedAsync`). Authorization
  attributes and policies are not used. A failed `Ensure*` produces 403 problem details that list
  the missing permissions.
- An option the user explicitly requested but lacks permission for MUST fail with 403, never be
  skipped silently. An automatic side effect the user lacks permission for is skipped.
- Every route requires an authenticated user with a tenant, except an explicit, reviewed
  allow-list.
- Every service MUST have a test that discovers all routes, calls each as a user with no roles,
  and expects 403 (allow-list excepted). A route that is missing its permission check MUST fail CI.
- Query responses MUST include boolean action flags (`canCreate`, `canEdit`, `canCancel`, ...) at
  screen and item level, combining permission with resource state. UIs show and enable controls
  only from these flags. The server still enforces every action.

Rationale: authorization stays with the service that owns the data, so Identity stays decoupled
from every service.

### VII. Tests Isolated from Development (NON-NEGOTIABLE)

- Tests MUST NOT touch the development Docker environment, its databases, broker, volumes or
  ports.
- Integration tests use Testcontainers with random ports and data on `tmpfs`, keep the Ryuk
  reaper enabled, and dispose containers in fixtures.
- End-to-end tests run in the separate compose project `e2e-test`, using only anonymous volumes or
  `tmpfs`. The runner MUST always execute `docker compose -p e2e-test down -v --remove-orphans`,
  whether the tests pass or fail. CI does the same in an `if: always()` step.
- After every test run, no containers or volumes labelled `com.docker.compose.project=e2e-test`
  or by Testcontainers may remain, and an automated check MUST verify this.
- Test stack: xUnit v3, Testcontainers, Respawn, Wolverine tracked sessions for messaging, and
  Playwright for end-to-end tests. The domain is covered by plain unit tests with no
  infrastructure.

### VIII. Everything Runs in Docker

- Every runtime component runs as a container: services, gateway, both SPAs (nginx), databases,
  broker, observability stack and, from phase 5, Redis.
- Each service owns its `compose.yaml` (API plus its database). `deploy/compose.yaml` composes
  them with `include:` and the profiles `infra`, `apps`, `observability` and `cache`.

### IX. Code Without Comments

Source code MUST NOT contain comments of any kind. Intent is expressed through naming, small
focused types and tests. This applies to C#, TypeScript, HTML, SCSS, Dockerfiles, YAML and
scripts.

### X. No Attribution

Commits, pull requests, source files and documentation MUST NOT contain AI or tool attribution
lines (e.g. `Co-Authored-By`, session links, "Generated with" footers).

## Architecture Constraints

| Area | Decision |
|---|---|
| Purpose | Portfolio/learning project: polish over feature count |
| Services (v1) | YARP Gateway, Identity, Product, Inventory, Order, Payment (fake provider with configurable failure rate and latency) |
| Services (later) | Shipping → Notification → Reporting |
| Frontends | Two Angular SPAs, `backoffice` (tenant staff) and `storefront` (customers, tenant slug in the URL path `/shop/<tenant>/...`), each in its own nginx container with its own OpenIddict client |
| SPA auth | Authorization code + PKCE. Tokens are sent through the gateway. 15-minute access tokens, refresh-token rotation, security stamp re-checked at refresh, no immediate revocation |
| Request flow | Gateway validates the JWT (401). The service validates again via cached JWKS, sets the tenant context, and the handler checks permissions (403). The tenant filter makes cross-tenant access 404 |
| User awareness | Identity publishes `TenantCreated`, `UserRegistered` and `UserDisabled`. Services keep a local user table and grant their admin role to a new tenant's first admin |
| Messaging | Wolverine with RabbitMQ transport, explicit routing per integration event, durable inbox and outbox. MassTransit and MediatR are not used |
| Persistence | EF Core with PostgreSQL in every service. Marten is not used |
| Sync vs async | Events by default. Consumers keep local read models (e.g. Order keeps product prices) |
| Background work | Wolverine scheduled and timeout messages. Hangfire is not used |
| Caching | Redis from phase 5 only, for HybridCache L2 on the catalog and gateway rate limiting |
| Resilience | Microsoft.Extensions.Http.Resilience (Polly v8) |
| API versioning | Asp.Versioning with a URL segment (`/v1/`) |
| Observability | OpenTelemetry Collector → Prometheus, Tempo and Loki → Grafana. Serilog writes through OpenTelemetry |
| Repository | Monorepo. `src/services/<name>/` holds `<Name>.slnx`, `<Name>.Api`, `<Name>.Contracts`, `<Name>.Tests`, `Directory.Packages.props`, `Dockerfile` and `compose.yaml`. `src/building-blocks/`, `src/gateway/` and `src/web/{backoffice,storefront}`. `deploy/` for root compose and observability configuration. `tests/e2e/`. A root `InventoryAndOrder.slnx` exists for the IDE only |
| CI | GitHub Actions, one workflow per service with `paths:` filters. Packages are packed before services are built. The feed may move to GitHub Packages in the deployment phase |
| Runtime | .NET 10 / ASP.NET Core, Angular |

Items to verify in the first feature that touches them: Wolverine discovers `Handle` on classes
derived from the generic base handlers. `MapPostToWolverine` returns the first tuple element as the
response and cascades `OutgoingMessages`. Tenant and user propagation through message headers is
implemented as Wolverine middleware.

## Development Workflow & Quality Gates

- Every change, however small, goes through Spec Kit: `/speckit-specify` → (`/speckit-clarify`)
  → `/speckit-plan` → `/speckit-tasks` → (`/speckit-analyze`) → `/speckit-implement`. There are
  no ad-hoc edits outside a feature.
- Implementation starts only after the spec, plan and tasks have been reviewed by the project owner.
- Delivery follows phases: 1 Foundation (compose, gateway, identity, databases), 2 Core services
  (Product, Inventory, Order), 3 Messaging (integration events, outbox), 4 Business workflows
  (Payment, saga, compensation), 5 Infrastructure (Redis, observability, resilience), 6 Quality
  (unit, integration, end-to-end), 7 Deployment (CI/CD, later Kubernetes).
- A plan MUST include a Constitution Check that confirms compliance with every principle, or
  documents and justifies any deviation in its complexity tracking.
- Merge gates: build, unit tests, integration tests, the no-roles 403 route test and the test
  cleanup check all pass for every affected service.

## Governance

This constitution supersedes all other practices in this repository. Plans, tasks and reviews
MUST verify compliance with it.

- Amendments are made only through `/speckit-constitution`, with the project owner's approval,
  and are recorded with a version bump and an updated amendment date.
- Versioning follows semantic versioning: MAJOR for removing or redefining principles, MINOR for
  new principles or materially expanded guidance, PATCH for clarifications.
- Any deviation from a principle MUST be justified in the feature plan's complexity tracking and
  approved by the project owner before implementation.

**Version**: 1.0.0 | **Ratified**: 2026-11-23 | **Last Amended**: 2026-11-23
