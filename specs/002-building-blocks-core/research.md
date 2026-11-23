# Research: BuildingBlocks Core

Versions verified on 2026-11-23.

## R1. Package split

- **Decision**: Two packages under the `building-blocks/v` prefix:
  - `BuildingBlocks.Core`: no package or framework references beyond the BCL (`System.Security.Claims` is part of it).
  - `BuildingBlocks.AspNetCore`: `FrameworkReference Microsoft.AspNetCore.App` and a package reference to Core.
- **Rationale**: Domain libraries and later message handlers can depend on Core without the web stack (FR-005, SC-005). Inside the repository, Core is a project reference from AspNetCore (constitution I applies to services, not to the projects inside the building-blocks folder). When packed, it becomes a normal package dependency on the same version.

## R2. Two-line adoption with correct middleware order

- **Problem**: The exception handler must run first, so errors before authentication are mapped (edge case). The identity check and tenant population must run after the service's `UseAuthentication()`. A single call can't sit in both positions.
- **Decision**:
  - `builder.Services.AddBuildingBlocks()` registers the problem-details services, the exception handler, the current user and the tenant context, plus an `IStartupFilter` that puts `UseExceptionHandler()` at the very start of the pipeline.
  - `app.UseBuildingBlocks()` is called after `UseAuthentication()` and adds the identity-validation and tenant middleware.
- **Rationale**: Exactly 2 lines (SC-002), and the ordering is always correct.
- **Alternatives considered**: One call that also calls `UseAuthentication()` itself. Rejected because authentication setup belongs to feature 007 and must stay under each service's control.

## R3. Problem details

- **Decision**: Use `AddProblemDetails()` plus one `IExceptionHandler` (`BuildingBlocksExceptionHandler`) that writes through `IProblemDetailsService`.
  - `traceId` = `Activity.Current?.Id ?? HttpContext.TraceIdentifier`. It is added by a `CustomizeProblemDetails` hook, so the 401 response from the identity middleware gets one too.
  - `type` uses the framework's default RFC 9110 links for each status.
  - `code` is an extension member.
- **Rationale**: Standard RFC 9457 output and a single code path (FR-008, SC-003).

## R4. Logging levels (FR-009)

- **Decision**:
  - 500 → `LogError` with the exception and the trace id.
  - Domain Rule, Conflict and NotFound → `LogInformation`, with the code and trace id and no stack trace.
  - Identity rejected (401) → `LogWarning`, with the reason and no claim values.
- **Rationale**: Domain errors are expected outcomes, not faults. Claim values could carry personal data.

## R5. Identity validation (FR-006)

- **Decision**: For authenticated principals, the middleware parses the claims once into an immutable `CurrentUser` stored in `HttpContext.Features`. It rejects with 401 (problem code `invalid-identity`) when:
  - `sub` is not a GUID
  - `user_type` is missing or unknown (parsed case-insensitively)
  - the user type is staff or customer and there is no `tenant_id`

  For unauthenticated principals it does nothing; endpoint authorization (feature 007) decides.
- **Rationale**: Bad identities are rejected before any handler, the claims are parsed once, and handlers get a typed object.

## R6. Tenant context (FR-003, FR-007)

- **Decision**: `TenantContext` is a scoped class implementing `ITenantContext`, with `Set(string tenantId)` and set-once semantics. Setting the same value again is a no-op. A different value throws `InvalidOperationException`. The middleware sets it from `CurrentUser.TenantId` when present.
- **Rationale**: Each request scope gets its own instance, so concurrent requests are isolated by DI scoping (US2 scenario 5). The same type serves message processing in feature 005.

## R7. Domain errors (FR-004)

- **Decision**:
  - `DomainException(string code, DomainErrorKind kind, string message)`. A null or whitespace code throws `ArgumentException`.
  - `enum DomainErrorKind { Conflict, Rule }`
  - `NotFoundException(string resource)` with `Code = "not-found"`. It is a separate type, not derived from `DomainException`, so not-found can never be confused with a rule error.
  - The 404 `detail` is a generic "The requested {resource} was not found.", which never reveals other tenants' data.

## R8. Test tooling

- **Decision**: `BuildingBlocks.Core.Tests` (pure unit tests) and `BuildingBlocks.AspNetCore.Tests`, which use `Microsoft.AspNetCore.TestHost` 10.0.15 in process (no Docker, no ports). Authentication is simulated with a test authentication handler that issues a principal built from headers. xUnit v3 4.0.3 on Microsoft.Testing.Platform, matching feature 001.
- **Packability**: `src/building-blocks/Directory.Build.props` sets `IsPackable=true` only when the project name does not end in `.Tests`. Test projects therefore never reach the feed.
- **CI**: The `building-blocks` workflow gains a step `dotnet test --solution src/building-blocks/BuildingBlocks.slnx`.
