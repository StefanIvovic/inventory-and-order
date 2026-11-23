---

description: "Task list for feature 005 BuildingBlocks Testing"
---

# Tasks: BuildingBlocks Testing

**Input**: Design documents from `specs/005-building-blocks-testing/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Every acceptance scenario is automated. The container tests need Docker.

**Global rules**: No comments. Pinned versions only. No image literals in test sources (FR-009). Nothing left behind.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Add `RYUK_IMAGE=testcontainers/ryuk:0.14.0` to `deploy/versions.env` (data-model.md "versions.env entries", FR-005). Confirm with `dotnet run --project tools/RepoChecks -- images` (exit 0)
- [X] T002 [P] Add `PackageVersion` entries to `src/building-blocks/Directory.Packages.props`: Testcontainers.PostgreSql 4.17.0, Testcontainers.RabbitMq 4.17.0, Npgsql 10.0.4, RabbitMQ.Client 7.3.0 and xunit.v3.extensibility.core 4.0.3 (research.md table)
- [X] T003 Create the projects:
  - `src/building-blocks/BuildingBlocks.Testing/BuildingBlocks.Testing.csproj`, referencing the Testcontainers packages, Npgsql and xunit.v3.extensibility.core
  - `src/building-blocks/BuildingBlocks.Testing.Tests/BuildingBlocks.Testing.Tests.csproj`: `OutputType` Exe, xunit.v3, `Using Xunit`, RabbitMQ.Client, and a reference to Testing

  Add both to `src/building-blocks/BuildingBlocks.slnx` and `InventoryAndOrder.slnx`

---

## Phase 2: Foundational

- [X] T004 Create the helper `src/building-blocks/BuildingBlocks.Testing.Tests/DockerCli.cs`, which runs `docker` with arguments and returns stdout and the exit code. It supports `inspect`, `ps` and `run`. It is used for assertions that Testcontainers doesn't expose (mounts, existence after dispose, the reaper image)

---

## Phase 3: User Story 1 - Tests run the same image versions as dev (Priority: P1) 🎯 MVP

**Goal**: One reader for pinned images

**Independent Test**: `ImageVersionsTests`

- [X] T005 [P] [US1] Write `src/building-blocks/BuildingBlocks.Testing.Tests/ImageVersionsTests.cs`:
  - the real repository file is found by walking up, and `Get("POSTGRES_IMAGE")` equals the line in `deploy/versions.env`
  - an unknown key throws, naming the key and the path
  - parsing an explicit temporary file works through a parser entry point that takes the file contents
  - the `IAO_VERSIONS_FILE` override and the walk-up locator are exercised through an internal `Locate(start, environmentValue)` that is visible to tests

  Image-like text is built by concatenation (data-model.md "ImageVersions lookup")
- [X] T006 [US1] Implement `src/building-blocks/BuildingBlocks.Testing/ImageVersions.cs` per research.md R1, with `InternalsVisibleTo` for the tests. Then make T005 pass

---

## Phase 4: User Story 2 - Throwaway PostgreSQL and RabbitMQ (Priority: P1)

**Goal**: Ready-to-use fixtures with pinned images, random ports, tmpfs and explicit disposal

**Independent Test**: `PostgresFixtureTests` and `RabbitMqFixtureTests`

- [X] T007 [P] [US2] Write `src/building-blocks/BuildingBlocks.Testing.Tests/PostgresFixtureTests.cs` for spec US2 scenarios 1 and 3–6 (data-model.md "Test container" `Image`, `Host port`, `Storage`, `Lifetime`):
  - `SELECT 1` works
  - `docker inspect` shows the image equals `ImageVersions.Get("POSTGRES_IMAGE")`
  - the host port is not 5432
  - the mounts contain a tmpfs at `/var/lib/postgresql` and no volume
  - two `CreateDatabaseAsync` calls give different databases: a table created in one is not visible in the other
  - after `DisposeAsync` on a fixture started inside the test, `docker ps -a --filter id=` is empty
- [X] T008 [P] [US2] Write `src/building-blocks/BuildingBlocks.Testing.Tests/RabbitMqFixtureTests.cs` for spec US2 scenarios 2, 3, 4 and 6:
  - a `RabbitMQ.Client` connection and channel open
  - the image is pinned
  - the port is not 5672
  - a tmpfs at `/var/lib/rabbitmq`, no volume
  - the container is removed after disposal
- [X] T009 [US2] Implement `src/building-blocks/BuildingBlocks.Testing/PostgresFixture.cs` and `RabbitMqFixture.cs` per research.md R3 and contracts/testing-api.md. Each constructor calls `TestInfrastructure.EnsureConfigured()` (a stub until US4). Then make T007 and T008 pass

**Checkpoint**: Integration tests can get a database and a broker with 2 lines (SC-003)

---

## Phase 5: User Story 3 - Nothing is left behind (Priority: P1)

**Goal**: An automated leftover check that also runs in CI when tests fail

**Independent Test**: quickstart.md steps 2–3

- [X] T010 [US3] Implement `scripts/check-test-cleanup.ps1` per research.md R4 and data-model.md "Cleanup check result": poll until clean or the timeout passes, list leftovers, use exit codes 0, 1 and 2
- [X] T011 [US3] Validate quickstart.md steps 2–3: after the test run the check exits 0, and with a manually labelled probe container it exits 1 and lists it. Remove the probe afterwards
- [X] T012 [US3] Update `.github/workflows/building-blocks.yml`:
  - a step that pre-pulls the three test images read from `deploy/versions.env` (POSTGRES, RABBITMQ, RYUK), with no literals
  - a `Test cleanup check` step with `if: always()`, running `pwsh ./scripts/check-test-cleanup.ps1`

---

## Phase 6: User Story 4 - Pinned reaper image (Priority: P2)

**Goal**: The reaper uses `RYUK_IMAGE`

**Independent Test**: `ReaperImageTests`

- [X] T013 [P] [US4] Write `src/building-blocks/BuildingBlocks.Testing.Tests/ReaperImageTests.cs`. After a fixture starts, the running reaper container (found by `docker ps --filter label=org.testcontainers.resource-reaper-session`, or by its image name) runs exactly `ImageVersions.Get("RYUK_IMAGE")`. If the reaper is disabled or not running in the environment (see the research.md R2 risk), the test fails with a message explaining why, rather than silently passing
- [X] T014 [US4] Implement `src/building-blocks/BuildingBlocks.Testing/TestInfrastructure.cs` (research.md R2). Then make T013 pass

---

## Phase 7: Polish

- [X] T015 Mutation checks:
  - make the Postgres fixture skip the tmpfs → the mount test fails
  - remove the reaper pin → T013 fails
  - remove the dispose call in the fixture → the disposal test fails (the reaper then removes the leftover, so the cleanup check is green, which is correct)
  - result: all three caught. The reaper mutation needed an extra settings-level test, because the library default equals our pin (research.md R2)

  Restore all three
- [X] T016 Run quickstart.md steps 1–7 on a clean clone in a temporary folder and delete it. Steps 5–6 cover the pack and consumer check for `BuildingBlocks.Testing`. Confirm the cleanup check is green afterwards
- [X] T017 Review for comments. Commit with no attribution, push, open the PR, and confirm CI passes, including the cleanup check step

---

## Dependencies & Execution Order

- Setup → Foundational → US1 → US2 (the fixtures need `ImageVersions`) → US3 → US4 → Polish
- [P]: T002 with T001. The test-writing tasks T007, T008 and T013 can be drafted in parallel

## Implementation Strategy

MVP is US1 plus US2. US3 makes isolation verifiable. US4 closes the last unpinned image.
