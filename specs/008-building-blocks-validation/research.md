# Research: BuildingBlocks Validation

Verified on 2026-11-23 against NuGet metadata and the Wolverine docs (main branch):

| Package | Version | License |
|---|---|---|
| FluentValidation | 12.2.0 | Apache-2.0 |
| WolverineFx.FluentValidation | 6.65.1 | MIT |

## R1. Which Wolverine integration

- **Decision**: `WolverineFx.FluentValidation` (message-handler middleware) with `opts.UseFluentValidation()`.
- **Rationale**: Our HTTP routes are `MapPostToWolverine` mediator routes (decision B17). They invoke the message-handler pipeline, so a single middleware covers both HTTP-invoked commands and broker messages. `WolverineFx.Http.FluentValidation` applies only to Wolverine HTTP endpoint methods, which we don't use.
- **Behaviour (from the docs)**:
  - the validators run before the handler
  - on failure a `FluentValidation.ValidationException` is thrown
  - the middleware applies only to message types that have validators
  - several validators are combined
  - a policy discards the message on `ValidationException`. **Overridden by owner decision D1 (2026-11-24)**: `UseBuildingBlocksValidation()` adds `opts.Policies.OnException<ValidationException>().MoveToErrorQueue()`, so invalid messages go to dead-letter storage (the `wolverine_dead_letters` table in the service's message store) instead of being discarded. Whether this policy wins over the discard policy, and in which registration order, is verified by the T010 test
- **Discovery**: the default `RegistrationBehavior.DiscoverAndRegisterValidators` scans the application assembly. Validators must be **public** for codegen, and must have no IoC dependencies, to avoid service location (decision `NotAllowed`, feature 006).

## R2. HTTP mapping through an extension point

- **Problem**: `ValidationException` reaches the 002 `BuildingBlocksExceptionHandler`, which maps unknown exceptions to 500. `BuildingBlocks.AspNetCore` must not depend on FluentValidation.
- **Decision**: Add `IExceptionProblemMapper` to `BuildingBlocks.AspNetCore`, with `bool TryMap(Exception exception, HttpContext context, out MappedProblem problem)`. `MappedProblem` carries the status, code, detail, extensions and log level. `BuildingBlocksExceptionHandler` consults all registered mappers first, then its built-in table (FR-006).
- **Validation mapper**: `ValidationProblemMapper` lives in the new package. It maps `ValidationException` to 400, `validation-failed`, `errors`, at Information log level.
- **Rationale**: Mappers open the error handling for extension without modifying it, so future packages can add mappings the same way.

## R3. Field naming

- **Decision**: Convert `ValidationFailure.PropertyName` to camel case per path segment, keeping the indexers: `Lines[1].Sku` → `lines[1].sku`, `WarehouseId` → `warehouseId`. Failures are grouped by the converted name. Messages keep their order, and duplicates are removed (FR-003).
- **Rationale**: This matches System.Text.Json's default camel-case naming used for request bodies, so the UI can match keys to form fields.
- **Messages**: FluentValidation's default messages name the property (for example "'Quantity' must be greater than '0'.") and never contain the attempted value unless a rule is written to include it. This is documented as a rule for validators (FR-007).

## R4. Setup

- **Decision**: `opts.UseBuildingBlocksValidation()` on `WolverineOptions` calls `UseFluentValidation()` and registers `ValidationProblemMapper` as an `IExceptionProblemMapper` (FR-001, SC-003).

## R5. Tests without Docker

- **Decision**: A WebApplication on TestServer with `UseWolverine` and no persistence or transports (in-memory local queues), plus `WolverineFx.RuntimeCompilation`.
  - **HTTP path**: `MapPostToWolverine` routes.
  - **Message path**: `InvokeAsync`, to check that the handler is not called, and a tracked `SendAsync` to a local queue, to check that the message is discarded and not retried.

## R6. Message contents in failure logs (found during implementation)

- **Finding**: Wolverine's executor logs a failed inline invocation as `"Invocation of {Message} failed!"` at the handler's `FailureLogLevel`, which defaults to Error, with the **message object**. Commands are records, so `ToString()` prints every property. A test caught `ManualThing { Value = VALUE-SECRET-42 }` in an Error log. Every failing command would log its contents, personal data included, so this applies to all services, not only validation.
- **Decision**: `opts.Policies.MessageFailureLogLevel(LogLevel.None)` in both `UseBuildingBlocksMessaging` and `UseBuildingBlocksValidation`. Failures remain logged without bodies: the HTTP path through the BuildingBlocks error handler (trace id and exception), and the message path through Wolverine's message-failed log (type, id, exception).
- **Tests**: the validation manual case asserts the secret never appears in any log, and the messaging failing-handler case asserts that the failed command's field value (`boom`) never appears.
