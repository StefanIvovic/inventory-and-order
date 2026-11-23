---

description: "Task list for feature 006 BuildingBlocks Messaging"
---

# Tasks: BuildingBlocks Messaging

**Input**: Design documents from `specs/006-building-blocks-messaging/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Every acceptance scenario is automated against real PostgreSQL and RabbitMQ containers (FR-010).

**Global rules**: No comments. Pinned versions. No image literals in tests. No manual `SaveChangesAsync` in handlers. Nothing left behind.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Add `PackageVersion` entries to `src/building-blocks/Directory.Packages.props` for WolverineFx, WolverineFx.EntityFrameworkCore, WolverineFx.Postgresql, WolverineFx.RabbitMQ and WolverineFx.Http (all 6.65.1), and Npgsql.EntityFrameworkCore.PostgreSQL 10.0.4 (research.md table)
- [X] T002 Create the projects:
  - `src/building-blocks/BuildingBlocks.Messaging/BuildingBlocks.Messaging.csproj`, referencing the four Wolverine packages and the AspNetCore and EntityFrameworkCore projects
  - `src/building-blocks/BuildingBlocks.Messaging.Tests/BuildingBlocks.Messaging.Tests.csproj`: Exe, xunit.v3, `Using Xunit`, WolverineFx.Http, Npgsql EF, RabbitMQ.Client, TestHost, and references to Messaging and Testing

  Add both to `src/building-blocks/BuildingBlocks.slnx` and `InventoryAndOrder.slnx`

---

## Phase 2: Foundational

- [X] T003 [P] Create `src/building-blocks/BuildingBlocks.Core/Handlers/IEventContext.cs` and `MessageHandler.cs` (research.md R6, contracts/messaging-api.md)
- [X] T004 [P] Create `src/building-blocks/BuildingBlocks.Messaging/MessagingSettings.cs`, which validates `ServiceName`, `PostgresConnectionString` and `RabbitMqUri` per data-model.md MessagingSettings, and `MessageHeaders.cs`
- [X] T005 Implement `src/building-blocks/BuildingBlocks.Messaging/MessagingExtensions.cs`, the `UseBuildingBlocksMessaging` method from research.md R1, wiring in R4 and R5 (filled in by later tasks). Implement `RoutingExtensions.cs` per research.md R3 and data-model.md "Broker topology" (exchange `Name` and `Type`, queue `Name`, `Binding`)
- [X] T006 Create the test infrastructure:
  - `src/building-blocks/BuildingBlocks.Messaging.Tests/MessagingFixture.cs`: one shared PostgreSQL container and one RabbitMQ container per test assembly
  - `TestServiceHost.cs`: builds a WebApplication on TestServer as a named "service", with its own fresh database, `TestDbContext : TenantDbContext`, `AddBuildingBlocks`, `UseBuildingBlocksMessaging`, `DurabilityMode.Solo`, a configurable routing callback, the test auth handler, and `MapWolverineEndpoints`
  - `TestModel.cs`: the `Widget` tenant-owned entity, and test events with explicit identities `test.widget-created.v1` and `test.widget-audited.v1`, valid per data-model.md "Message identity"

**Checkpoint**: A test service host starts against real containers

---

## Phase 3: User Story 1 - All or nothing per handler (Priority: P1) 🎯 MVP

**Goal**: Entity changes and outgoing events commit together, or not at all

**Independent Test**: `TransactionTests`

- [X] T007 [P] [US1] Write `src/building-blocks/BuildingBlocks.Messaging.Tests/TransactionTests.cs`, using two hosts (a publisher service and a subscriber service):
  - a handler that adds a `Widget` and returns `WidgetCreated`: the widget is persisted, and the subscriber handles the event once
  - the same handler failing after the add: zero widgets, the subscriber handles nothing, and the tracked session records no sent message
  - the handler never calls `SaveChangesAsync` itself (SC-001)
  - broker outage, using a dedicated `RabbitMqFixture` instance in its own test collection so other tests are unaffected:
    - `docker pause` the broker, then run the successful handler: the widget is committed and nothing is delivered
    - `docker unpause`: the stored event is delivered once (spec US1 scenario 3, covering the edge case of a broker that is down at startup as well)
- [X] T008 [US1] Complete the transaction and outbox wiring in `MessagingExtensions.cs`, then make T007 pass

---

## Phase 4: User Story 2 - Handled once, even if delivered twice (Priority: P1)

**Goal**: Durable inbox deduplication by message id

**Independent Test**: `InboxTests`

- [X] T009 [P] [US2] Write `src/building-blocks/BuildingBlocks.Messaging.Tests/InboxTests.cs`:
  - RabbitMQ.Client publishes the same raw Wolverine message (same `id` header, same `message-type`, same body) twice to the subscriber's queue, and the handler count is 1
  - two raw messages with different ids and identical bodies give a count of 2

  (SC-002, data-model.md "Envelope metadata" `Id`)
- [X] T010 [US2] Confirm that the inbox configuration from T005/T008 makes T009 pass. Adjust `MessagingExtensions.cs` if it doesn't (research.md R1)

---

## Phase 5: User Story 3 - Events reach exactly the subscribers (Priority: P1)

**Goal**: Fanout per identity, one copy per subscribing service

**Independent Test**: `RoutingTests`

- [X] T011 [P] [US3] Write `src/building-blocks/BuildingBlocks.Messaging.Tests/RoutingTests.cs`:
  - a publisher, subscribers S1 and S2, and a non-subscriber S3: one publish gives S1=1, S2=1 and S3=0
  - the subscriber handler records `Envelope.MessageType`, which equals `test.widget-created.v1`
  - `ExchangeDeclarePassiveAsync("test.widget-created.v1")` over RabbitMQ.Client succeeds
  - a subscriber started before the publisher has ever published still receives the first event

  (SC-003, data-model.md "Message identity" `Identity`, "Broker topology")
- [X] T012 [US3] Make T011 pass by adjusting `RoutingExtensions.cs` if needed

---

## Phase 6: User Story 4 - Tenant, user and correlation everywhere (Priority: P1)

**Goal**: Event handlers run in the originating tenant, and know the initiating user and correlation id

**Independent Test**: `PropagationTests`

- [X] T013 [P] [US4] Write `src/building-blocks/BuildingBlocks.Messaging.Tests/PropagationTests.cs`:
  - an HTTP POST by staff `u1` / `acme` invokes a command that publishes `WidgetCreated`
  - the subscriber's `MessageHandler`-derived handler records `Tenant.TenantId`, `InitiatedByUserId` and `CorrelationId`, counts tenant-filtered widgets (only `acme` is visible), and cascades `WidgetAudited` to a third host
  - every hop has the same tenant, user and correlation id
  - a message published with no tenant reaches a handler that sees no tenant and zero tenant-owned rows

  (SC-004, data-model.md "Envelope metadata" `TenantId`, `user-id`, `CorrelationId`)
- [X] T014 [P] [US4] Implement `src/building-blocks/BuildingBlocks.Messaging/IdentityEnvelopeRule.cs` per research.md R4
- [X] T015 [P] [US4] Implement `src/building-blocks/BuildingBlocks.Messaging/TenantContextMiddleware.cs` per research.md R5
- [X] T016 [US4] Implement `src/building-blocks/BuildingBlocks.Messaging/WolverineEventContext.cs` (research.md R6). Register it, the rule, `PropagateIncomingHeaderToOutgoing(MessageHeaders.UserId)` and the middleware in `MessagingExtensions.cs`. Then make T013 pass

---

## Phase 7: User Story 5 - Command handlers and HTTP mapping (Priority: P2)

**Goal**: Close the B20 verification points

**Independent Test**: `HttpCommandTests`

- [X] T017 [P] [US5] Write `src/building-blocks/BuildingBlocks.Messaging.Tests/HttpCommandTests.cs`:
  - `CreateWidgetHandler : CommandHandler<TestDbContext>` is discovered with no registration
  - `app.MapPostToWolverine<CreateWidget, WidgetCreatedResponse>("/widgets")` returns the response body, persists the widget, and delivers `WidgetCreated` to a subscriber
  - a caller without `widgets.create` gets 403 `permission-denied`, with zero widgets and nothing delivered

  (research.md R7, R8)
- [X] T018 [US5] Make T017 pass. Wolverine-specific findings go into research.md

---

## Phase 8: Polish

- [X] T019 Mutation checks:
  - switch transactions off (remove `AutoApplyTransactions`) → T007's failure case fails
  - remove the tenant middleware → T013 fails
  - remove `IdentityEnvelopeRule` → T013 fails on the user and tenant of HTTP-originated messages
  - remove the durable inbox → T009's duplicate case fails

  Restore all four
- [X] T020 Add a `Consumer check Messaging` step to `.github/workflows/building-blocks.yml` (FR-011)
- [X] T021 Run quickstart.md steps 1–4 on a clean clone in a temporary folder, then delete it. The cleanup check must be green
- [X] T022 Review for comments. Commit with no attribution, push, open the PR, and confirm CI passes

---

## Dependencies & Execution Order

- Setup → Foundational → US1 → US2 → US3 → US4 → US5 → Polish. US1–US4 all touch `MessagingExtensions.cs`, so they run in sequence
- [P]: T003 with T004. Each story's test file can be drafted before its implementation

## Implementation Strategy

MVP is US1, the transactional outbox. US2–US4 complete the messaging guarantees, and US5 closes the B20 design checks.
