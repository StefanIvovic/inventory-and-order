# Implementation Plan: BuildingBlocks Messaging

**Branch**: `006-building-blocks-messaging` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-building-blocks-messaging/spec.md`

## Summary

This feature creates `BuildingBlocks.Messaging`, with:
- `UseBuildingBlocksMessaging(settings)`: PostgreSQL message storage, Eager EF Core transactions applied to every handler, a durable inbox and outbox everywhere, and RabbitMQ with automatic provisioning
- routing helpers: `PublishEvent<T>`, `SubscribeToEvent<T>` and `ListenToServiceQueue`
- `[MessageIdentity]`-based names
- propagation of tenant, user and correlation id: native Wolverine for message-to-message hops, plus an `IdentityEnvelopeRule` for messages that start in an HTTP request
- `TenantContextMiddleware`, which sets the tenant for every incoming message

It also adds `IEventContext` and `MessageHandler<TDbContext>` to Core. Everything is tested against
real PostgreSQL and RabbitMQ through feature 005's fixtures, with several Wolverine hosts acting as
services. The tests also cover the B20 points: discovery of derived handlers, and
`MapPostToWolverine` returning a response plus cascaded events.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401)

**Primary Dependencies**: WolverineFx, WolverineFx.EntityFrameworkCore, WolverineFx.Postgresql and WolverineFx.RabbitMQ 6.65.1, plus BuildingBlocks.AspNetCore and BuildingBlocks.EntityFrameworkCore. Tests also use WolverineFx.Http 6.65.1, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.4, RabbitMQ.Client 7.3.0 and BuildingBlocks.Testing

**Storage**: Each service's own PostgreSQL database, with a `wolverine` schema for envelopes

**Testing**: xUnit v3, Testcontainers through BuildingBlocks.Testing, and Wolverine tracked sessions

**Target Platform**: ASP.NET Core services

**Project Type**: NuGet library package

**Performance Goals**: Not a goal for this feature. Correctness first

**Constraints**:
- no manual `SaveChangesAsync` in handlers
- no `EnableRetryOnFailure`
- nothing left behind after tests
- pinned images
- no comments

**Scale/Scope**: About 10 types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Each service has its own queue, its own message storage schema in its own database, and explicit routing. No shared database |
| II. Shared code | ✅ | Infrastructure only. Event types stay in each service's Contracts package. Test events live only in tests |
| III. Handlers and domain | ✅ | `EventHandler` base. All logic stays in handlers |
| IV. Transactions and messaging | ✅ | Eager per-handler transactions, the outbox and inbox everywhere, delivery after commit |
| V. Multi-tenancy | ✅ | The tenant comes from the envelope for messages, and the middleware sets the tenant context before handlers run |
| VI. Authorization | ✅ | `EventHandler` has no permission access, and messages are not re-checked |
| VII. Tests isolated | ✅ | Testcontainers fixtures, a fresh database per test class, and the cleanup check in CI |
| VIII–X | ✅ | Pinned images through feature 005 |
| B10 explicit routing | ✅ | Fanout exchange per identity, and one bound queue per service |
| B20 verification | ✅ | US5 tests |

**Gate: PASS**, both before and after design.

## Project Structure

### Documentation (this feature)

```text
specs/006-building-blocks-messaging/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/messaging-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code

```text
src/building-blocks/
├── Directory.Packages.props                             (adds Wolverine, Npgsql EF and WolverineFx.Http)
├── BuildingBlocks.Core/Handlers/IEventContext.cs
├── BuildingBlocks.Core/Handlers/MessageHandler.cs
├── BuildingBlocks.Messaging/
│   ├── BuildingBlocks.Messaging.csproj
│   ├── MessagingSettings.cs
│   ├── MessageHeaders.cs
│   ├── MessagingExtensions.cs
│   ├── RoutingExtensions.cs
│   ├── IdentityEnvelopeRule.cs
│   ├── TenantContextMiddleware.cs
│   └── WolverineEventContext.cs
└── BuildingBlocks.Messaging.Tests/
    ├── MessagingFixture.cs
    ├── TestServiceHost.cs
    ├── TestModel.cs
    ├── TransactionTests.cs
    ├── InboxTests.cs
    ├── RoutingTests.cs
    ├── PropagationTests.cs
    └── HttpCommandTests.cs
.github/workflows/building-blocks.yml                    (adds the Messaging consumer check)
```

**Structure Decision**: Same pattern as 002–005.

## Complexity Tracking

No violations.
