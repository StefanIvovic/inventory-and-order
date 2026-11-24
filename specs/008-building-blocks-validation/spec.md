# Feature Specification: BuildingBlocks Validation

**Feature Branch**: `008-building-blocks-validation`

**Created**: 2026-11-23

**Status**: Draft

**Input**: User description: "Feature 008 of Phase 1. Input validation with FluentValidation (Apache-2.0, confirmed free), run by Wolverine before the handler. Invalid input never reaches the handler. An HTTP caller gets one 400 response listing all field errors at once, grouped by field, in the same error format as every other error (code and trace id). A ValidationException thrown manually is mapped the same way, as a safety net. Validators check input shape only. Business rules stay in the domain (DomainException → 409/422)."

## User Scenarios & Testing *(mandatory)*

The users are the developers of every service and, through them, the UI, which needs to mark every invalid field at once.

### User Story 1 - All field errors at once (Priority: P1)

A client submits a command with three invalid fields. It gets one 400 response that lists all three, grouped by field and in client-facing field names, so the UI can mark every field in one pass.

**Why this priority**: This is what the owner asked for: all errors at once, not one by one.

**Independent Test**: Post a command with several invalid fields through a mapped route, and read the response.

**Acceptance Scenarios**:

1. **Given** a command with an empty `name`, a negative `quantity` and two errors on `warehouseId`, **When** it is posted, **Then** the response is 400 with `errors` containing exactly those three fields: one message for `name`, one for `quantity`, two for `warehouseId`.
2. **Given** any validation failure, **When** the response is produced, **Then** it has the standard shape: `type`, `title`, `status` 400, `code` `validation-failed`, `errors` and `traceId`.
3. **Given** a nested field such as the SKU of the second order line, **When** it fails, **Then** its key uses the client's naming: `lines[1].sku`.
4. **Given** a valid command, **When** it is posted, **Then** validation lets it through and the handler runs normally.

---

### User Story 2 - Invalid input never reaches the handler (Priority: P1)

Validation runs before the handler. An invalid command or message is rejected without the handler running, so nothing is saved and no event is published.

**Why this priority**: Handlers must be able to trust their input. Running a handler on invalid input and failing halfway wastes work and invites partial side effects.

**Independent Test**: Count handler invocations for valid and invalid input, over HTTP and as a message.

**Acceptance Scenarios**:

1. **Given** an invalid command posted over HTTP, **When** it is rejected, **Then** the handler ran 0 times, nothing is persisted and no event is published.
2. **Given** an invalid message delivered to a message handler, **When** it is processed, **Then** the handler runs 0 times, the message is not retried, and it is stored in the dead-letter storage.
3. **Given** a message type with no validator, **When** it is handled, **Then** no validation overhead applies and the handler runs.

---

### User Story 3 - The same 400 for manual validation (Priority: P2)

When a developer validates something manually inside a handler and throws the standard validation exception, the client gets exactly the same 400 shape.

**Why this priority**: It is a safety net that keeps one error format. It is P2 because the middleware covers the normal path.

**Independent Test**: A handler that throws a validation exception manually returns the same shape as US1.

**Acceptance Scenarios**:

1. **Given** a handler that throws the validation exception with two failures, **When** the response is produced, **Then** it is the same 400 shape with both fields.
2. **Given** a validation failure, **When** the logs are inspected, **Then** it is logged at information level with the trace id. It is never an error, and no field values are logged.

---

### User Story 4 - One-line adoption, with a clear boundary (Priority: P2)

A service turns on validation with one setup call. Its validators are found automatically. The documentation states the boundary: validators check shape (required fields, lengths, ranges, formats), and business rules remain domain errors.

**Why this priority**: Consistency across services. It is P2 because the behaviour is covered by US1–US3.

**Independent Test**: A test application with one setup call and a validator in its assembly validates without any other registration.

**Acceptance Scenarios**:

1. **Given** a service calling the validation setup, **When** a validator exists for a command, **Then** it is applied with no manual registration.
2. **Given** the 400 and 422 responses, **When** the documentation is read, **Then** it states that 400 means malformed input (validators) and 422 means a broken business rule (domain).

### Edge Cases

- Several validators for one command. All of them run, and all errors are combined.
- A validator that needs injected services. The documentation discourages this, because validators must stay pure and Wolverine's code generation forbids hidden service location. Data-dependent checks belong in the handler or the domain.
- The same message is both an HTTP command and a message elsewhere. The same validator applies to both paths.
- Error messages contain no field values, so personal data is never echoed into logs.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A new shared validation package MUST provide one setup call that enables FluentValidation for Wolverine handlers, discovers the service's validators, and runs them before handlers.
- **FR-002**: An invalid command or message MUST NOT reach its handler. Over HTTP, the client gets 400. A message from the broker or a durable queue is not retried. It is moved to the service's dead-letter storage, logged as a warning, and kept for inspection and replay, because an invalid integration event is a contract bug and its evidence must survive (owner decision, 2026-11-24).
- **FR-003**: The 400 response MUST contain all failures of all validators for the input, grouped by field. Field names are converted to the client's camel-case naming, including nested and indexed paths.
- **FR-004**: The 400 response MUST follow the common error format (`type`, `title`, `status`, `traceId`) with `code` `validation-failed` and an `errors` object of field → messages.
- **FR-005**: A validation exception thrown anywhere in request handling MUST be mapped to the same 400.
- **FR-006**: The HTTP error mapping from feature 002 MUST gain an extension point, so additional exception types can be mapped by other packages without the core package depending on them.
- **FR-007**: Validation failures MUST be logged at information level with the trace id. The framework MUST never log submitted values. Validator messages must not include them either, which is a quickstart rule for validator authors.
- **FR-008**: The package MUST be packable under `building-blocks/v`, with a CI consumer check, and its tests MUST run without Docker.

### Key Entities

- **Validator**: the shape rules for one command or message type.
- **Validation failure**: one field, one message.
- **Validation problem**: the 400 response that aggregates all failures.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of a command's validation failures appear in a single 400 response.
- **SC-002**: 0 handler invocations for invalid input, on both HTTP and message paths.
- **SC-003**: A service enables validation with 1 setup line and no per-validator registration.
- **SC-004**: All acceptance scenarios are automated and pass in CI. The HTTP and in-process tests need no Docker. The dead-letter test uses the PostgreSQL fixture from feature 005, because dead-letter storage is durable.

## Assumptions

- FluentValidation 12.2.0 (Apache-2.0) and WolverineFx.FluentValidation 6.65.1 (MIT).
- English messages. Localization and error codes per field are out of scope.
- HTTP and inline tests run Wolverine in memory. The dead-letter test uses durable PostgreSQL storage from the feature 005 fixtures.
