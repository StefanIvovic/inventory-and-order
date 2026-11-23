---

description: "Task list for feature 001 Package Pipeline"
---

# Tasks: Package Pipeline

**Input**: Design documents from `specs/001-package-pipeline/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included for the RepoChecks image rules (SC-007). The scripts and build rules are verified through quickstart steps.

**Organization**: Tasks are grouped by user story. Story numbers match spec.md: US1 pack, US2 consumer, US3 build rules, US4 image pins, US5 CI gate.

**Global rules for every task**:
- No comments in any file written. This is authoring discipline (constitution IX), and no tooling checks it.
- Every version comes from research.md and is pinned exactly.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository skeleton and SDK pin

- [X] T001 Create folders `deploy/`, `scripts/`, `src/building-blocks/`, `tools/` and `.github/workflows/`, and replace the `artifacts/packages/` entry in `.gitignore` with `artifacts/` (FR-001)
- [X] T002 [P] Create `global.json` pinning SDK `10.0.401` with `rollForward` `disable` (data-model.md SDK pin `sdk.version` and `sdk.rollForward`)
- [X] T003 [P] Create `nuget.config` (research.md R5):
  - clear all sources, then add `local` = `artifacts/packages` and `nuget.org`
  - `packageSourceMapping` routes the shared package id patterns of data-model.md Shared package `Id` (`BuildingBlocks.*`, `*.Contracts`) to `local` only, and `*` to `nuget.org`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Baseline build rules that every project inherits

- [X] T004 Create root `Directory.Build.props` (FR-003, data-model.md Shared package `IsPackable`):
  - `TargetFramework` net10.0, `Nullable` enable, `ImplicitUsings` enable
  - `TreatWarningsAsErrors` true, `AnalysisLevel` latest-recommended, `Deterministic` true
  - `IsPackable` false by default
  - `ContinuousIntegrationBuild` true when `GITHUB_ACTIONS` is `true`
- [X] T005 Create root `Directory.Build.targets` with a target that runs before `MinVer` and errors with the project name when `IsPackable` is `true` and `MinVerTagPrefix` is empty (data-model.md Shared package `TagPrefix`, research.md R2, FR-005)
- [X] T006 Create root `InventoryAndOrder.slnx` for IDE use only. Projects are added as they are created

**Checkpoint**: `dotnet --version` reports 10.0.401 in the repository root

---

## Phase 3: User Story 1 - Produce versioned shared packages locally (Priority: P1) 🎯 MVP

**Goal**: One command packs every shared package into the local feed, with versions taken from git

**Independent Test**: quickstart.md steps 5, 6 and 9

- [X] T007 [US1] Create `src/building-blocks/Directory.Build.props`: import the parent props, set `IsPackable` true and `MinVerTagPrefix` `building-blocks/v` (data-model.md Tag prefix families, and Shared package `IsPackable` and `TagPrefix`)
- [X] T008 [US1] Create `src/building-blocks/Directory.Packages.props` with `ManagePackageVersionsCentrally` true and `MinVer` 8.0.0 as a `GlobalPackageReference` with `PrivateAssets` all (research.md R1)
- [X] T009 [US1] Create the empty class library `src/building-blocks/BuildingBlocks.Core/BuildingBlocks.Core.csproj`, with no source files and no version properties (FR-007, data-model.md Shared package `Id` and `Version`)
- [X] T010 [US1] Create `src/building-blocks/BuildingBlocks.slnx` containing BuildingBlocks.Core, and add the project to `InventoryAndOrder.slnx`
- [X] T011 [US1] Implement `scripts/pack.ps1` per contracts/pack-cli.md and research.md R4:
  - discover packable projects under `src/` with `dotnet msbuild -getProperty:IsPackable`
  - create the output folder, then run `dotnet pack` for each project
  - clear the matching prerelease versions from the global packages folder
  - refuse to overwrite a release version whose content hash differs
  - print one `id version` line per package and exit with the contract codes
- [X] T012 [US1] Validate quickstart.md steps 5, 6 and 9, including the under-2-minute timing (SC-001), and confirm the versions follow data-model.md Version states

**Checkpoint**: The feed contains `BuildingBlocks.Core` with a git-derived version

---

## Phase 4: User Story 2 - Consume a package from the local feed in isolation (Priority: P1)

**Goal**: Show that a project outside the source tree can consume a shared package by pinned version only

**Independent Test**: quickstart.md steps 7 and 8

- [X] T013 [US2] Implement `scripts/check-consumer.ps1` per contracts/check-consumer-cli.md:
  - create a temporary folder under the system temp path, with a consumer `.csproj` that pins the version and a `nuget.config` with the feed and source mapping
  - set `NUGET_PACKAGES` to a temporary cache, then run restore and build
  - in `finally`, delete the consumer and the cache and verify they are gone
  - default to the highest feed version
- [X] T014 [US2] Validate quickstart.md steps 7 and 8, including that no temporary folders remain after both the pass and the fail case

**Checkpoint**: Package-only consumption is proven, and the check cleans up in every outcome

---

## Phase 5: User Story 4 - One pinned version per component (Priority: P1)

**Goal**: A single definition of image versions, enforced by the `images` check. The SDK image is tied to `global.json`

**Independent Test**: quickstart.md step 4, plus the seeded image tests

### Tests for User Story 4

- [X] T015 [P] [US4] Create `tools/RepoChecks.Tests/RepoChecks.Tests.csproj` with xUnit v3 4.0.3 on Microsoft.Testing.Platform via `tools/Directory.Packages.props` (research.md R11 revision), and a `TempRepo` fixture that writes files into a unique temporary folder and deletes it in `Dispose` (research.md R11)
- [X] T016 [P] [US4] Write the seeded tests in `tools/RepoChecks.Tests/Images/ImageRulesTests.cs`:
  - one passing case and one failing case for each rule `IP001`–`IP008` in contracts/repochecks-cli.md
  - forbidden forms:
    - `latest`, an untagged image, `postgres:18` and `rabbitmq:4.4`
    - a literal compose tag, an undefined variable, and a Dockerfile ARG with a default
    - an image literal in test source
    - an SDK tag that doesn't match `global.json`
    - two keys for the same repository
  - also check that `postgres:18.7-alpine3.24` is accepted (major.minor is the full Postgres version)
  - fixture content is built from C# string literals, and image references in the test code are built by concatenating parts so the tool's own `IP006` scan doesn't flag them

### Implementation for User Story 4

- [X] T017 [US4] Create the `tools/RepoChecks/RepoChecks.csproj` console tool and `tools/Directory.Packages.props` with Microsoft.CodeAnalysis.CSharp 5.12.0 and YamlDotNet 18.1.0. Create `tools/RepoChecks.slnx` containing both tool projects, and add them to `InventoryAndOrder.slnx`
- [X] T018 [US4] Implement `tools/RepoChecks/Program.cs` per contracts/repochecks-cli.md: the `images` command, the option `--root`, the output format `<path>:<line>: <rule-id> <detail>`, exit codes 0, 1 and 2, and the scanned-file set and exclusions
- [X] T019 [P] [US4] Implement the `versions.env` parser and rules `IP001`, `IP002` and `IP008` in `tools/RepoChecks/Images/VersionsFile.cs` (data-model.md Image version entry `Key` and `Value`, contracts/versions-env.md, and the research.md R7 list of components that require a patch version)
- [X] T020 [P] [US4] Implement the compose rules `IP003` and `IP004` using YamlDotNet in `tools/RepoChecks/Images/ComposeRule.cs`
- [X] T021 [P] [US4] Implement the Dockerfile rules `IP004` and `IP005` in `tools/RepoChecks/Images/DockerfileRule.cs`
- [X] T022 [P] [US4] Implement the test-source literal rule `IP006` with Roslyn string-literal tokens in `tools/RepoChecks/Images/TestSourceRule.cs`
- [X] T023 [P] [US4] Implement the SDK consistency rule `IP007` in `tools/RepoChecks/Images/SdkRule.cs` (data-model.md SDK pin invariant and `sdk.version`)
- [X] T024 [US4] Create `deploy/versions.env` with exactly the four entries in contracts/versions-env.md
- [X] T025 [US4] Run `dotnet test tools/RepoChecks.slnx` (all tests pass) and quickstart.md step 4 (exit 0)

**Checkpoint**: Image versions are defined once, and any drift or duplicate fails the check

---

## Phase 6: User Story 3 - Uniform build rules (Priority: P2)

**Goal**: Every project inherits the baseline rules

**Independent Test**: quickstart.md step 3

- [X] T026 [US3] Validate quickstart.md step 3: a scratch project with no settings inherits the baseline from `Directory.Build.props`, and a nullable warning fails its build. Delete the scratch project afterwards

**Checkpoint**: The baseline applies to new projects with no local configuration

---

## Phase 7: User Story 5 - Pull request gate for shared packages (Priority: P3)

**Goal**: The PR shows pass or fail for the tool tests, image check, pack and consumer steps

**Independent Test**: quickstart.md step 10

- [X] T027 [US5] Create `.github/workflows/building-blocks.yml` per research.md R10:
  - `paths` filters for `pull_request` and `push` to `master`
  - `actions/checkout@v7.0.1` with `fetch-depth: 0`
  - `actions/setup-dotnet@v6.1.0` with `global-json-file: global.json`
  - steps: tool tests, `images`, `pwsh scripts/pack.ps1`, `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.Core`
- [X] T028 [US5] Run the `images` check locally over the whole repository, including the new workflow, with exit 0

**Checkpoint**: Every change to shared packages gets an automatic gate

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T029 Run the full quickstart.md (steps 1–9) end to end on a clean clone in a temporary folder, record the pack timing for the PR description, then delete the clone
- [ ] T030 Review every added file for comments (authoring rule IX). Commit on `001-package-pipeline` with no attribution, push, open the PR against `master` with the timing and the analyze results, and confirm the workflow passes (quickstart.md step 10)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup. Blocks every story
- **US1 (Phase 3)**: After Foundational
- **US2 (Phase 4)**: After US1, because it needs a package in the feed
- **US4 (Phase 5)**: After Foundational. Independent of US1 and US2
- **US3 (Phase 6)**: After Foundational
- **US5 (Phase 7)**: After US1, US2 and US4, because the workflow runs all of them
- **Polish (Phase 8)**: Last

### Parallel Opportunities

- T002 and T003
- US1/US2, US4 and US3 can proceed in parallel after Phase 2
- Inside US4: T015 and T016, then T019–T023

## Parallel Example: User Story 4

```text
Task: "Implement versions.env parser in tools/RepoChecks/Images/VersionsFile.cs"
Task: "Implement compose rules in tools/RepoChecks/Images/ComposeRule.cs"
Task: "Implement Dockerfile rules in tools/RepoChecks/Images/DockerfileRule.cs"
Task: "Implement test-source rule in tools/RepoChecks/Images/TestSourceRule.cs"
Task: "Implement SDK rule in tools/RepoChecks/Images/SdkRule.cs"
```

## Implementation Strategy

1. Phases 1–2, then US1 (MVP: packages in the feed)
2. US2, which proves package-only consumption
3. US4 (the image-pin check), then US3 validation
4. US5 CI gate, then Polish and the PR

## Notes

- Tests are written before their implementation, and are expected to fail first.
- No task touches Docker. Container use begins in features 004 and 005.
