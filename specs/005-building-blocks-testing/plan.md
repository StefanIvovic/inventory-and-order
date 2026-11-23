# Implementation Plan: BuildingBlocks Testing

**Branch**: `005-building-blocks-testing` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-building-blocks-testing/spec.md`

## Summary

This feature adds a packable `BuildingBlocks.Testing` with:
- `ImageVersions`, which reads `deploy/versions.env`
- `TestInfrastructure`, which pins the Ryuk reaper image
- `PostgresFixture` (with `CreateDatabaseAsync`) and `RabbitMqFixture`, both with random ports, tmpfs storage and explicit disposal

It also adds `RYUK_IMAGE` to `versions.env`, a `scripts/check-test-cleanup.ps1` that polls Docker
for leftovers, and CI steps that pre-pull the images and always run the cleanup check. This is the
first feature whose tests need Docker.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.401), PowerShell 7

**Primary Dependencies**: Testcontainers.PostgreSql and Testcontainers.RabbitMq 4.17.0, Npgsql 10.0.4, xunit.v3.extensibility.core 4.0.3. Tests also use RabbitMQ.Client 7.3.0

**Storage**: Throwaway containers with tmpfs storage

**Testing**: xUnit v3 4.0.3 on Microsoft.Testing.Platform, with real Docker

**Target Platform**: Developer machines with Docker Desktop, and GitHub `ubuntu-latest`

**Project Type**: NuGet library package, a script, and CI changes

**Performance Goals**: Containers start once per test class or assembly, not once per test

**Constraints**: Pinned images only (constitution VIII). No named volumes. Random ports. Nothing left behind (constitution VII)

**Scale/Scope**: About 5 public types and 1 script

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I and II | ✅ | A shared testing package consumed by pinned version. Infrastructure only |
| VII. Tests isolated from dev | ✅ | Random ports, tmpfs, no named volumes, explicit disposal, the reaper as a safety net, and an automated leftover check that runs even when tests fail. This implements E1/E1a directly |
| VIII. Pinned images | ✅ | Every image, including the reaper, comes from `deploy/versions.env`. The image-pin check covers the new entry |
| IX and X | ✅ | — |
| Others | n/a | — |

**Gate: PASS**, both before and after design.

## Project Structure

### Documentation (this feature)

```text
specs/005-building-blocks-testing/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/testing-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code

```text
deploy/versions.env                                   (adds RYUK_IMAGE)
scripts/check-test-cleanup.ps1
.github/workflows/building-blocks.yml                 (pre-pull and cleanup steps)
src/building-blocks/
├── Directory.Packages.props                          (adds Testcontainers, Npgsql, RabbitMQ.Client, xunit.v3.extensibility.core)
├── BuildingBlocks.slnx and InventoryAndOrder.slnx    (2 projects added)
├── BuildingBlocks.Testing/
│   ├── BuildingBlocks.Testing.csproj
│   ├── ImageVersions.cs
│   ├── TestInfrastructure.cs
│   ├── PostgresFixture.cs
│   └── RabbitMqFixture.cs
└── BuildingBlocks.Testing.Tests/
    ├── DockerCli.cs
    ├── ImageVersionsTests.cs
    ├── PostgresFixtureTests.cs
    ├── RabbitMqFixtureTests.cs
    └── ReaperImageTests.cs
```

**Structure Decision**: The same layout as the earlier BuildingBlocks packages.

## Complexity Tracking

No violations.
