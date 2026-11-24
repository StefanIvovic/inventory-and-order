# Feature Specification: BuildingBlocks Analyzers

**Feature Branch**: `009-building-blocks-analyzers`

**Created**: 2026-11-24

**Status**: Draft

**Input**: User description: "Feature 009 of Phase 1. Two correctness rules that were review-only become compile errors in every service, shipped with the BuildingBlocks packages:
- IAO1001: using IPermissionCache outside BuildingBlocks, because invalidation must go through the PermissionAssignmentsChanged event after commit
- IAO1002: calling SaveChanges or SaveChangesAsync inside a Wolverine handler, because the transaction middleware saves and commits

Both must be precise (no false positives outside their scope) and must reach services without any setup."

## User Scenarios & Testing *(mandatory)*

The users are service developers, who get the error in the IDE and the build, and reviewers, who no longer have to catch these by eye.

### User Story 1 - Direct permission cache use is a compile error (Priority: P1)

A developer who injects or references the permission cache in service code gets a build error explaining that permission changes must publish the change event instead.

**Why this priority**: A direct invalidation runs before commit and only on the local instance. That is the defect fixed in feature 007, and it is easy to reintroduce because the interface is public.

**Independent Test**: Compile service-like code that references the cache in several ways. Each reference gives the error. The same code inside the BuildingBlocks assemblies does not.

**Acceptance Scenarios**:

1. **Given** a handler with a constructor parameter of the permission cache type, **When** it compiles, **Then** the build fails with IAO1001 at that parameter.
2. **Given** code that resolves the cache from a service provider, or uses it as a generic type argument, a field type, a local variable type or in `typeof`, **When** it compiles, **Then** each occurrence gives IAO1001.
3. **Given** code inside a BuildingBlocks assembly, **When** it compiles, **Then** there is no IAO1001.
4. **Given** service code that only publishes the change event, **When** it compiles, **Then** there is no diagnostic.

---

### User Story 2 - Saving inside a handler is a compile error (Priority: P1)

A developer who calls the database context's save methods inside a Wolverine handler gets a build error explaining that the transaction middleware saves and commits. The same call in non-handler code (seeding, migrations, design-time factories, tests) stays allowed.

**Why this priority**: A manual save inside a handler breaks the documented transaction boundary. It is harmless in Eager mode but commits early in Lightweight mode.

**Independent Test**: Compile handlers that save in various shapes (direct, awaited, inside a lambda or local function within the handler), plus non-handler code that saves.

**Acceptance Scenarios**:

1. **Given** a `Handle` method of a class derived from the command or message handler base that calls `SaveChangesAsync`, **When** it compiles, **Then** IAO1002 is reported at the call.
2. **Given** a static handler class whose name ends in `Handler` or `Consumer`, with a public `Handle`, `Handles`, `Consume` or `Consumes` method that calls `SaveChanges`, **When** it compiles, **Then** IAO1002 is reported.
3. **Given** a save call inside a lambda or local function within a handler method, **When** it compiles, **Then** IAO1002 is reported.
4. **Given** a save call in a non-handler class (a seeder, a factory or a test helper), or in a non-handler method of a handler class, **When** it compiles, **Then** there is no diagnostic.
5. **Given** a method named `SaveChangesAsync` on a type that is not a database context, called inside a handler, **When** it compiles, **Then** there is no diagnostic.

---

### User Story 3 - The rules reach every service with no setup (Priority: P1)

A service that references any of the BuildingBlocks web, persistence or messaging packages from the feed gets both rules automatically. It needs no extra package reference or configuration.

**Why this priority**: A rule that must be switched on manually will eventually be missing in some service.

**Independent Test**: A temporary consumer project outside the repository references only the messaging package from the feed, plus a violating file. Its build fails with both rule ids. The same project without the violations builds.

**Acceptance Scenarios**:

1. **Given** a consumer referencing only BuildingBlocks.Messaging from the feed, **When** it contains both violations, **Then** its build fails, reporting IAO1001 and IAO1002.
2. **Given** the same consumer without violations, **When** it builds, **Then** it succeeds.
3. **Given** BuildingBlocks.Core, **When** its package is inspected, **Then** it still has zero dependencies (feature 002 SC-005).

### Edge Cases

- A handler that receives the context through `CommandHandler<T>.Db` and saves through it is caught, because the receiver type is a database context.
- A method named like a handler on a type that is not a handler (for example `OrderValidator.Handle`) is not flagged.
- A suppression with a justification is visible in review. The rule documentation states when one is acceptable, which is almost never.
- Generated code (Wolverine's generated handlers) is excluded from analysis.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A new analyzer assembly MUST report IAO1001 (error) for every reference to the permission cache type in assemblies whose name does not start with `BuildingBlocks.`. The message names the change event as the alternative.
- **FR-002**: The analyzer MUST report IAO1002 (error) for every call to `SaveChanges` or `SaveChangesAsync`, with a receiver deriving from the EF Core database context, located inside a handler method or in a lambda or local function within one. A handler method is one of:
  - a public `Handle`, `Handles`, `Consume` or `Consumes` method of a type that derives from the command or message handler base
  - such a method on a type whose name ends in `Handler` or `Consumer`
  - such a method on a type marked with Wolverine's handler attribute or implementing Wolverine's handler interface
- **FR-003**: Both rules MUST skip generated code, and MUST have documentation (title, message and help text) explaining the correct pattern.
- **FR-004**: The analyzers MUST flow to consuming services automatically from the BuildingBlocks AspNetCore, EntityFrameworkCore, Messaging and Validation packages. BuildingBlocks.Core MUST keep zero dependencies.
- **FR-005**: The analyzer MUST load in the pinned SDK's compiler and in IDEs, so it references a Roslyn version no newer than the SDK compiler.
- **FR-006**: Both rules MUST be covered by automated tests (positive and negative cases), and by an end-to-end check that builds a consumer from the feed. The tests run without Docker. The end-to-end check runs in CI.
- **FR-007**: The repository's own code MUST compile cleanly with the analyzers active.

### Key Entities

- **Diagnostic rule**: an id, a severity, a message and help text.
- **Handler method**: the scope in which IAO1002 applies.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the listed positive cases are reported, and 0 of the listed negative cases.
- **SC-002**: A consumer referencing one BuildingBlocks package from the feed gets both rules with 0 lines of configuration.
- **SC-003**: The repository builds with 0 analyzer diagnostics.

## Assumptions

- Roslyn 4.14.0 is referenced, older than the 5.9.0 compiler of SDK 10.0.401. Analyzers target `netstandard2.0`, as Roslyn requires.
- Interprocedural cases (a handler calling a helper that saves) are out of scope and remain a review item.
- English messages.
