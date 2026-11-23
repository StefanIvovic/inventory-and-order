# Research: Permission Invalidation

## R1. Event and identity

- **Decision**: `BuildingBlocks.Messaging.PermissionAssignmentsChanged(string? TenantId, Guid UserId)` with
  `[MessageIdentity("building-blocks.permission-assignments-changed.v1")]` (FR-001).
- **Rationale**: This is infrastructure, not a service's domain event, so it lives in BuildingBlocks
  (constitution II allows infrastructure). Its identity is fixed, and only the destination differs per service.

## R2. Broadcast topology

- **Decision**: `opts.UseBuildingBlocksPermissionInvalidation()`, called after `UseBuildingBlocksMessaging`:
  - Destination: a fanout exchange `{ServiceName}.permission-assignments`, declared through `ConfigureRabbitMq().DeclareExchange`. `PublishMessage<PermissionAssignmentsChanged>().ToRabbitExchange(...)` sends there.
  - Per-instance queue: `{ServiceName}.permission-assignments.{instanceId}`, with `instanceId = Guid.NewGuid():N`
    per process. It is configured through `ListenToRabbitQueue(name, q => { q.IsDurable = false; q.IsExclusive = true; q.AutoDelete = true; })`
    and bound to the exchange. Wolverine's `RabbitMqQueue` exposes those three properties.
  - Handler: `PermissionAssignmentsChangedHandler.Handle(event, IPermissionCache)` → `Invalidate(TenantId, UserId)`.
    It is included with `opts.Discovery.IncludeType<PermissionAssignmentsChangedHandler>()`.
- **Rationale**:
  - **FR-004**: an exclusive, auto-deleting queue disappears with its connection, so stopped instances leave nothing behind. RabbitMQ 4.4 allows non-durable queues only when they are exclusive (finding from feature 006).
  - **FR-005**: the exchange is per service.
  - The publishing instance also receives its own copy, so it invalidates itself after commit too.

## R3. Ordering

- **Decision**: The role-assignment handler returns `PermissionAssignmentsChanged` as a cascaded message. Feature 006's Eager transaction plus the outbox write it in the same commit, and it is dispatched only after the commit (FR-003). A rolled-back handler never emits it (US2 scenario 1).

## R4. Inbox on the instance queue (superseded by R4a: buffered, no durable inbox)

- **Note**: `UseDurableInboxOnAllListeners()` from feature 006 also applies here. That is acceptable: invalidation is idempotent, and the inbox only adds a row per message. If Wolverine rejects a durable inbox on a non-durable queue, the listener is set to buffered (in-memory) explicitly. Any such finding is recorded here during implementation.
- **Balanced mode**: if an instance crashes, its stored inbox rows may be recovered by another instance. That is harmless, because that instance invalidates its own cache, which its own copy already did.

## R4a. Findings during implementation

- **Codegen variable collision.** A handler that injects `ITenantContext`, while the 006 middleware injected the concrete `TenantContext`, produced generated code with two variables named `tenantContext` (CS0128). Any service handler injecting `ITenantContext` would have failed. **Fix:** `TenantContextMiddleware` takes `ITenantContext` and sets it through the concrete type, which is the same scoped instance. `AlwaysUseServiceLocationFor<TenantContext>` was removed, so only `ITenantContext` is service-located.
- **Durable inbox breaks broadcast within one service.** Instances of a service share one database. A fanout delivers the *same* envelope id to every instance queue, and the shared durable inbox treated every copy after the first as a duplicate and dropped it. Only one instance invalidated, and the others waited for cache expiry (the test took 61 s and hit the 60 s TTL). **Fix:** the per-instance listener is `.BufferedInMemory()`, which overrides the global durable-inbox policy. Invalidation is idempotent and the queue is ephemeral, so no durability is lost. **Lesson for later features:** any same-service broadcast must not use the durable inbox. Cross-service fanout is unaffected, because each service has its own database.
- **Exclusive queues** answer a passive declare from another connection with `RESOURCE_LOCKED` (405). That is proof of exclusivity. After the owner stops, the reply is `NOT_FOUND` (404).

## R5. Test approach

- **Two instances of one service** share one PostgreSQL database and use `DurabilityMode.Balanced`, the real multi-node mode. Solo assumes a single node.
- **One more service** with a different name, checking FR-005.
- **The permission provider** reads `PermissionGrant` rows from the database and counts its calls in a singleton per host.
- **The `/permissions` endpoint** warms and inspects caches through HTTP with test authentication.
- **Queue cleanup (SC-004)**: after stopping an instance, `QueueDeclarePassiveAsync` on its queue name fails with NOT_FOUND.

## R6. Constitution amendment (v1.1.0 → v1.2.0, minor)

- **New rule** under principle VI: permission caches are invalidated only by publishing `PermissionAssignmentsChanged` from the handler that changed the assignments, which means after commit through the outbox. Handlers never call `IPermissionCache` directly.
- **Principle III**: `EventHandler<TDbContext>` becomes `MessageHandler<TDbContext>`, and the message context becomes `IEventContext`.
- **Architecture constraints**: Wolverine code generation is runtime (Dynamic, with `WolverineFx.RuntimeCompilation` in Debug only) in development and tests, and pre-generated (Static, committed, with a CI diff guard and a Static smoke start) in production. This applies to every service project from its creation, first in feature 011 (identity-authentication, after the roadmap renumbering on 2026-11-23). It is a decision, not a merge gate for the current repository, which has no service yet.
