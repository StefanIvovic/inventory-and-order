# Research: BuildingBlocks Messaging

Sources: the Wolverine documentation from the `JasperFx/wolverine` repository (main branch), read on
2026-11-23, and NuGet metadata. The docs may be slightly ahead of 6.65.1, so the compiler and the
tests are the final check for every API below.

| Package | Version | License | Notes |
|---|---|---|---|
| WolverineFx, WolverineFx.EntityFrameworkCore, WolverineFx.Postgresql, WolverineFx.RabbitMQ | 6.65.1 | MIT | net10.0. The EF integration needs EF Core ≥ 10.0.4, and we use 10.0.15 |
| WolverineFx.Http | 6.65.1 | MIT | Tests only, for verifying B20 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.4 | — | Tests only. Stays on the 10.x line to match EF Core 10. 11.0.0 exists but goes with EF 11 |

## R1. One setup call

- **Decision**: `WolverineOptions.UseBuildingBlocksMessaging(MessagingSettings settings)` with these steps:
  - `opts.ServiceName = settings.ServiceName`
  - `opts.PersistMessagesWithPostgresql(settings.PostgresConnectionString, "wolverine")`
  - `opts.UseEntityFrameworkCoreTransactions()`, which defaults to **Eager** mode: an explicit transaction, so a failing handler rolls back everything, including `ExecuteUpdate`
  - `opts.Policies.AutoApplyTransactions()`
  - `opts.Policies.UseDurableInboxOnAllListeners()`, `UseDurableOutboxOnAllSendingEndpoints()` and `UseDurableLocalQueues()`
  - `opts.UseRabbitMq(settings.RabbitMqUri).AutoProvision()`
  - the propagation and middleware described in R4 and R5
- **Rationale**: FR-001 and FR-002. The docs confirm that the outbox rows are written by the same `SaveChangesAsync` as the entities, in both modes.
- **Constraint**: `EnableRetryOnFailure` on the DbContext is incompatible with Eager mode, and Wolverine rejects the combination at startup. Services must not enable it. This is documented in the quickstart.
- **Tests**: Use `opts.Durability.Mode = DurabilityMode.Solo` for fast single-node hosts.

## R2. Message identity

- **Decision**: Integration events use Wolverine's `[MessageIdentity("domain.event-name.v1")]` attribute, with the version included in the name string. That gives one explicit identity per contract version (FR-005, constitution II).
- **Exchange names** come from `typeof(T).ToMessageTypeName()`, which honours the attribute.

## R3. Routing

- **Decision**: Extension methods on `WolverineOptions`:
  - `PublishEvent<T>()` → `opts.PublishMessage<T>().ToRabbitExchange(identity)`. The exchange is fanout, which is the default.
  - `SubscribeToEvent<T>()` → `rabbit.DeclareExchange(identity, e => e.BindQueue(serviceQueue))`
  - `ListenToServiceQueue()` → `opts.ListenToRabbitQueue(serviceQueue).UseDurableInbox()`
  - service queue name = `ServiceName`
- **Rationale**: Every subscribing service gets one copy on its own queue, and the instances of a service compete for it. Both sides declare the exchange, so start order doesn't matter (US3 scenario 3).

## R4. Tenant, user and correlation propagation

- **Tenant**: Wolverine tracks `Envelope.TenantId` natively and copies it onto cascaded messages.
- **Correlation**: `Envelope.CorrelationId` is propagated natively.
- **User**: `opts.Policies.PropagateIncomingHeaderToOutgoing("user-id")` handles message-to-message hops.
- **Messages that start in an HTTP request** have no incoming envelope. A custom `IEnvelopeRule`, `IdentityEnvelopeRule`, is registered through `opts.MetadataRules.Add(...)`:
  - in `ApplyCorrelation` and `Modify`, when the outgoing `TenantId` is null, it is set from the authenticated `CurrentUser` found through `IHttpContextAccessor`
  - when the `user-id` header is missing, it is set the same way
- **Header names**: `user-id`. The tenant uses Wolverine's built-in tenant header.

## R4a. Wolverine's default tenant marker (found during implementation)

- When an HTTP route invokes a command without a tenant, Wolverine stamps the envelope with `JasperFx.StorageConstants.DefaultTenantId` (`"*DEFAULT*"`), and cascades inherit it. Event handlers then saw the tenant `*DEFAULT*`.
- **Decision**: `EnvelopeTenancy.Normalize` treats null, whitespace and the default marker as "no tenant". The envelope rule replaces it with the real tenant, and the middleware falls back to the HTTP user.

## R5. Incoming tenant context

- **Decision**: `TenantContextMiddleware`, registered with `opts.Policies.AddMiddleware<TenantContextMiddleware>()`. Its `Before(Envelope envelope, TenantContext tenant)` calls `tenant.Set(envelope.TenantId)` when one is present (FR-007).
- **Inline invocations from HTTP**: the envelope tenant is either the same as the tenant already set from the HTTP user, which is a no-op, or absent, in which case it is skipped.

## R5a. Tenant context instance sharing (found during implementation)

- **Symptom**: Handlers failed with `TenantViolationException: Cannot insert Widget without a tenant`, although the middleware had set the tenant.
- **Evidence**: A diagnostic handler showed the envelope tenant was `acme`. With `ServiceLocationPolicy.AllowedButWarn`, the concrete `TenantContext` and `ITenantContext` became the same instance and both had the tenant.
- **Cause**: Wolverine 6 inlines constructors in generated code, and its default `ServiceLocationPolicy` is `NotAllowed`. The middleware received an inline `new TenantContext()`, while the DbContext received the request-scope instance through the opaque `ITenantContext` factory registration.
- **Decision**: `opts.CodeGeneration.AlwaysUseServiceLocationFor<TenantContext>()` and `AlwaysUseServiceLocationFor<ITenantContext>()`. This is the targeted escape hatch the Wolverine 6 docs recommend: only these types come from the scope, and the rest of the codegen stays inlined.

## R5b. Runtime compilation (found during implementation)

- Wolverine 6 core no longer ships the Roslyn runtime compiler. Dynamic codegen needs `WolverineFx.RuntimeCompilation` (MIT, 6.65.1), or the code must be pre-generated with `TypeLoadMode.Static`.
- **Decision**: The package does not choose for services. The test project references `WolverineFx.RuntimeCompilation`. Each service decides when it is created: dev and tests use runtime compilation, and production images are expected to use static code generation. This is an open item for feature 010.

## R6. Event-handler base and naming

- **Decision**:
  - `BuildingBlocks.Core.Handlers.IEventContext` exposes `ITenantContext Tenant`, `Guid? InitiatedByUserId`, `string? CorrelationId` and `TimeProvider Clock`
  - `MessageHandler<TDbContext>(IEventContext context, TDbContext db)` exposes `Context`, `Db`, `Tenant`, `InitiatedByUserId`, `CorrelationId` and `Clock`, and **no** `Permissions` member
- **Naming**: The design phase called it `IMessageContext`, but Wolverine already has `Wolverine.IMessageContext`. Our type is `IEventContext`, which avoids constant aliasing (FR-008).
- **Base class name (revised during implementation)**: `MessageHandler<TDbContext>`, not `EventHandler<TDbContext>`. `System.EventHandler<TEventArgs>` has the same arity and is always imported, which would give CS0104 ambiguity in every handler file. Analyzer CA1711 also forbids the suffix. The name pairs with `CommandHandler<TDbContext>`.
- **Implementation**: `WolverineEventContext` lives in the Messaging package and reads Wolverine's `IMessageContext.Envelope`.

## R7. Discovery of handlers derived from the base classes

- **From the docs**: Wolverine discovers public **concrete** classes whose names end in `Handler` or `Consumer`, using public instance `Handle` methods. Our abstract bases are skipped, and derived concrete handlers are found.
- **Constructor dependencies** (`IRequestContext`, `IEventContext`, the DbContext) are resolved from the container.
- **To verify**: US5 scenario 1.

## R8. HTTP mapping

- **From the docs**: `app.MapPostToWolverine<TRequest, TResponse>(path)`.
- **To verify** (US5 scenario 2): when the handler returns a tuple of `(TResponse, OutgoingMessages)`, the response goes into the body and the messages cascade. Tests reference WolverineFx.Http. The package itself does not.

## R9. Test approach

- **Containers**: `PostgresFixture` (with a fresh database per test class) and `RabbitMqFixture` from feature 005.
- **Hosts**: Several Wolverine hosts in one test process act as separate "services": a publisher, subscribers and a non-subscriber. Each has its own database.
- **Assertions**: Wolverine tracked sessions (`TrackActivity().IncludeExternalTransports().AlsoTrack(...)`) wait for cascades across hosts.
- **Duplicate delivery**: A real Wolverine-produced message is captured from a temporary exclusive queue bound to the event exchange, then republished to the service queue with RabbitMQ.Client.
- **Found during implementation**:
  - Wolverine's RabbitMQ mapping stores the envelope id in `BasicProperties.MessageId`, not in an `id` header, and the identity in `BasicProperties.Type`. The tenant travels in the `tenant-id` header.
  - RabbitMQ 4.4 rejects non-durable, non-exclusive queues (`transient_nonexcl_queues` is deprecated).
