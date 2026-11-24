# Implementation Plan: BuildingBlocks Validation

**Branch**: `008-building-blocks-validation` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/008-building-blocks-validation/spec.md`

## Summary

This feature adds an `IExceptionProblemMapper` extension point to the 002 exception handler, and
a new `BuildingBlocks.Validation` package. Its `UseBuildingBlocksValidation()` enables Wolverine's
FluentValidation middleware, so validators run before handlers and are discovered automatically.
It also registers a mapper that turns `ValidationException` into one 400 `validation-failed`
response, with all errors grouped by camel-cased field. Everything is tested in memory, with no
Docker.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401)

**Primary Dependencies**: FluentValidation 12.2.0 and WolverineFx.FluentValidation 6.65.1, plus BuildingBlocks.AspNetCore. Tests also use WolverineFx.Http, WolverineFx.RuntimeCompilation and TestHost

**Storage**: N/A

**Testing**: xUnit v3, TestServer, and in-memory Wolverine

**Target Platform**: ASP.NET Core services

**Project Type**: A new NuGet package plus a small extension to an existing one

**Performance Goals**: Validation runs only for message types that have validators

**Constraints**: AspNetCore must not depend on FluentValidation. No comments. No Docker

**Scale/Scope**: About 5 types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I and II | ✅ | A new infrastructure package. Validators stay in services |
| III. Handlers and domain | ✅ | Validators check input shape, and business rules stay in the domain. Handlers are never reached by invalid input |
| VI. Authorization | ✅ | Unchanged |
| VII. Tests isolated | ✅ | In memory, no Docker |
| 002 error format | ✅ | The same base shape, plus `errors` |
| Others | n/a | — |

**Gate: PASS**, both before and after design.

## Project Structure

```text
src/building-blocks/
├── Directory.Packages.props                                   (adds FluentValidation and WolverineFx.FluentValidation)
├── BuildingBlocks.AspNetCore/Errors/IExceptionProblemMapper.cs
├── BuildingBlocks.AspNetCore/Errors/MappedProblem.cs
├── BuildingBlocks.AspNetCore/Errors/BuildingBlocksExceptionHandler.cs (consults the mappers)
├── BuildingBlocks.AspNetCore.Tests/Errors/ProblemMapperTests.cs
├── BuildingBlocks.Validation/
│   ├── BuildingBlocks.Validation.csproj
│   ├── FieldNames.cs
│   ├── ValidationProblemMapper.cs
│   └── ValidationExtensions.cs
└── BuildingBlocks.Validation.Tests/
    ├── TestApp.cs
    ├── FieldNamesTests.cs
    ├── HttpValidationTests.cs
    └── MessageValidationTests.cs
.github/workflows/building-blocks.yml                           (adds the Validation consumer check)
```

## Complexity Tracking

No violations.
