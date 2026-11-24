# Implementation Plan: BuildingBlocks Analyzers

**Branch**: `009-building-blocks-analyzers` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/009-building-blocks-analyzers/spec.md`

## Summary

This feature adds `BuildingBlocks.Analyzers` (netstandard2.0, Roslyn 4.14.0) with two error rules:
- **IAO1001**: `IPermissionCache` used outside `BuildingBlocks.*` assemblies
- **IAO1002**: `SaveChanges`/`SaveChangesAsync` on a DbContext inside a Wolverine handler method, including nested lambdas and local functions

It is packed as its own analyzer package. The AspNetCore, EF, Messaging and Validation packages
reference it with `PrivateAssets=none`, so it flows to every service, while Core stays
dependency-free. It is tested with an offline in-repo compilation harness, plus an end-to-end
script that builds a consumer from the feed.

## Technical Context

**Language/Version**: C#. The analyzer is netstandard2.0, and the tests are net10.0

**Primary Dependencies**: Microsoft.CodeAnalysis.CSharp 4.14.0 (analyzer, PrivateAssets all). The tests reference EF Core 10.0.15 and WolverineFx 6.65.1 as metadata

**Storage**: N/A

**Testing**: xUnit v3, with an in-repo compilation harness

**Target Platform**: Compilers and IDEs that consume the BuildingBlocks packages

**Project Type**: Analyzer NuGet package, plus a script

**Performance Goals**: Analyzers are registered per syntax node or operation, with concurrent execution enabled

**Constraints**:
- Roslyn version ≤ the SDK compiler's (5.9.0)
- no false positives outside scope
- Core keeps zero dependencies
- no comments

**Scale/Scope**: 2 rules

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| II. Shared code | ✅ | Tooling package, infrastructure only |
| IV. Transactions | ✅ | Enforces "no manual SaveChangesAsync in handlers" |
| VI. Authorization | ✅ | Enforces the v1.2.0 invalidation rule |
| VII. Tests isolated | ✅ | Offline harness, and the end-to-end script cleans up |
| VIII. Pinned versions | ✅ | All package versions are pinned |
| IX and X | ✅ | — |

**Gate: PASS**, both before and after design.

## Project Structure

```text
src/building-blocks/
├── BuildingBlocks.Analyzers/
│   ├── BuildingBlocks.Analyzers.csproj
│   ├── DiagnosticIds.cs
│   ├── PermissionCacheUsageAnalyzer.cs
│   └── SaveChangesInHandlerAnalyzer.cs
├── BuildingBlocks.Analyzers.Tests/
│   ├── AnalyzerHarness.cs
│   ├── PermissionCacheUsageAnalyzerTests.cs
│   └── SaveChangesInHandlerAnalyzerTests.cs
├── BuildingBlocks.{AspNetCore,EntityFrameworkCore,Messaging,Validation}/*.csproj   (analyzer reference)
└── Directory.Packages.props                                                       (Roslyn 4.14.0 for the analyzer)
scripts/check-analyzers.ps1
.github/workflows/building-blocks.yml                                              (analyzer check step)
```

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| Two Roslyn versions in one repository: 4.14.0 for the analyzer, 5.12.0 for RepoChecks | An analyzer must not exceed the host compiler. RepoChecks runs as its own process | A single version would either break analyzer loading or make RepoChecks needlessly old |
