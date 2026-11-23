# Implementation Plan: Package Pipeline

**Branch**: `001-package-pipeline` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-package-pipeline/spec.md`

## Summary

This feature lays down the repository skeleton every later feature depends on:

- Shared build rules (`Directory.Build.props/.targets`, `global.json` with the SDK pinned exactly).
- A local NuGet folder feed with source mapping.
- Git-tag versioning through MinVer, with a fail-fast check for a missing tag prefix.
- A `pack.ps1` that builds every packable project into the feed and handles stale caches.
- A `check-consumer.ps1` that proves package-only consumption in a temporary location and always
  cleans up.
- `deploy/versions.env` as the single source of container image versions.
- A `RepoChecks` tool that enforces the image-pin rule. No comments is an authoring rule, so there is no tooling for it.
- One CI workflow that runs all of the above on pull requests.

The first real package is an empty `BuildingBlocks.Core`, which feature 002 fills.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401, pinned), PowerShell 7 for scripts

**Primary Dependencies**: MinVer 8.0.0, Microsoft.CodeAnalysis.CSharp 5.12.0, YamlDotNet 18.1.0

**Storage**: Files only (local feed `artifacts/packages/`, `deploy/versions.env`)

**Testing**: xUnit v3 4.0.3, Microsoft.NET.Test.Sdk 18.12.0, xunit.runner.visualstudio 4.0.1

**Target Platform**: Windows developer machine and GitHub-hosted Ubuntu runners

**Project Type**: Build tooling (scripts, MSBuild configuration and a CLI tool) plus one empty library package

**Performance Goals**: A full pack of all packages in under 2 minutes (SC-001). The image check finishes in seconds.

**Constraints**:
- No comments in any file this feature adds, including scripts and configuration.
- No dependency on Docker.
- Every action, package and image version is pinned exactly.

**Scale/Scope**: 1 package project now, growing to roughly 15 (BuildingBlocks.* and the contracts packages). The repository checks scan the whole repository.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | The pipeline is what makes package-only sharing possible. No service exists yet |
| II. Shared code via versioned packages | ✅ | Local feed, MinVer prefixes and source mapping. `BuildingBlocks.Core` is packable |
| III. Handlers / domain | n/a | No application code |
| IV. Transactions / messaging | n/a | — |
| V. Multi-tenancy | n/a | — |
| VI. Authorization | n/a | — |
| VII. Tests isolated from dev | ✅ | Tool tests use temporary folders only. The consumer check uses a temporary folder and an isolated NuGet cache, and always deletes both |
| VIII. Everything in Docker / pinned images | ✅ | Adds `deploy/versions.env` and the image-pin check. No containers run in this feature |
| IX. No comments | ✅ | An authoring discipline checked in review. No comment-detection tooling, by owner decision |
| X. No attribution | ✅ | Commit and PR text carry none |
| Workflow: one feature, one PR | ✅ | Feature 001 of 8 |

The `tools/` folder is repository tooling. Services never consume it, so principle II is not
affected. **Gate: PASS.**

## Project Structure

### Documentation (this feature)

```text
specs/001-package-pipeline/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── pack-cli.md
│   ├── check-consumer-cli.md
│   ├── repochecks-cli.md
│   └── versions-env.md
├── checklists/requirements.md
└── tasks.md                 (next step: /speckit-tasks)
```

### Source Code (repository root)

```text
global.json
nuget.config
Directory.Build.props
Directory.Build.targets
InventoryAndOrder.slnx
.gitignore
deploy/
└── versions.env
scripts/
├── pack.ps1
└── check-consumer.ps1
src/building-blocks/
├── BuildingBlocks.slnx
├── Directory.Build.props
├── Directory.Packages.props
└── BuildingBlocks.Core/
    └── BuildingBlocks.Core.csproj
tools/
├── RepoChecks.slnx
├── Directory.Packages.props
├── RepoChecks/
│   ├── RepoChecks.csproj
│   ├── Program.cs
│   └── Images/
└── RepoChecks.Tests/
    └── RepoChecks.Tests.csproj
.github/workflows/
└── building-blocks.yml
```

**Structure Decision**: The constitution's monorepo layout, created only as far as this feature
needs. `src/building-blocks/` and `tools/` each own their solution and their
`Directory.Packages.props`, so they build on their own. The root `InventoryAndOrder.slnx`
includes both, for the IDE only.

## Complexity Tracking

No violations.
