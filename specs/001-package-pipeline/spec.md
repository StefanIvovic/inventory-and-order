# Feature Specification: Package Pipeline

**Feature Branch**: `001-package-pipeline`

**Created**: 2026-11-23

**Status**: Draft

**Input**: User description: "First of eight Phase 1 (Foundation) features. Repository skeleton and shared-package pipeline: root layout, shared build settings, a local package feed, automatic package versioning from git tags, and a pack script. This lets every later service consume BuildingBlocks and contracts as versioned packages, never as project references. Addition: pin Docker image versions in one place, so the same component never runs at multiple versions."

## User Scenarios & Testing *(mandatory)*

The users of this feature are the developers of the platform (today, the project owner) and the CI pipeline.

### User Story 1 - Produce versioned shared packages locally (Priority: P1)

A developer runs one command from the repository root. Every shared package project in the repository is built and placed in the local package feed with a version derived automatically from git history. No version number is ever typed by hand.

**Why this priority**: Under the constitution, every later feature consumes shared code only as versioned packages. Without this pipeline no service can be built.

**Independent Test**: Run the pack command on a clean checkout. The feed folder then holds one package per shared package project, each with a version computed from git.

**Acceptance Scenarios**:

1. **Given** a clean checkout with no tags for a package, **When** the developer runs the pack command, **Then** the feed contains that package with an automatic prerelease version.
2. **Given** a tag `building-blocks/v0.1.0` on the current commit, **When** the developer runs the pack command, **Then** the BuildingBlocks packages are produced with version `0.1.0`, and packages with a different tag prefix are not affected.
3. **Given** the pack command has already run, **When** it runs again with no changes, **Then** it succeeds and the feed holds no duplicate or conflicting versions.

---

### User Story 2 - Consume a package from the local feed in isolation (Priority: P1)

A consuming project restores a shared package from the local feed by a pinned version, using only the repository's package configuration, with no project reference to the package's source.

**Why this priority**: This proves the independence rule (constitution I and II) works before any real service exists.

**Independent Test**: A throwaway consumer, created in a temporary location outside the source tree, restores the package by its pinned version from the feed and builds. The temporary consumer is deleted afterwards, whether the check passes or fails.

**Acceptance Scenarios**:

1. **Given** the feed contains a package, **When** a consumer pins that version and restores, **Then** restore resolves the package from the local feed and the build succeeds.
2. **Given** a consumer pins a version that is not in the feed, **When** it restores, **Then** restore fails with a clear "package version not found" error.
3. **Given** the consumer check finishes in any state, **When** it exits, **Then** no temporary consumer files remain.

---

### User Story 3 - Uniform build rules (Priority: P2)

Every project in the repository picks up the same baseline build rules automatically: target runtime, nullability, warnings treated as errors, and analyzers.

**Why this priority**: Consistency has to come from shared configuration from the first project. Otherwise each project drifts.

**Independent Test**: Add a project with no local settings. It builds with the baseline rules, and a nullable warning in it fails the build.

**Acceptance Scenarios**:

1. **Given** a new project file with no build settings of its own, **When** it builds, **Then** it uses the repository baseline rules.
2. **Given** a project with code that triggers a compiler warning, **When** it builds, **Then** the build fails because warnings are treated as errors.

---

### User Story 4 - One pinned version per component (Priority: P1)

All container image versions used anywhere in the platform are defined exactly once, as exact version tags. Everything that runs a container (dev compose, e2e, CI, Testcontainers) takes its version from that single definition, so the same component never runs at two different versions. The .NET SDK version is pinned the same way for every build.

**Why this priority**: Version drift between services, test environments and CI creates "works on my machine" failures. Pinning has to exist before the first container is introduced (features 004 and 005).

**Independent Test**: Seed a compose file and a Dockerfile that reference images correctly through the versions file: the image-pin check passes. Change one to `postgres:latest`, `postgres:18` or a literal tag: the check fails and names the file and line.

**Acceptance Scenarios**:

1. **Given** the versions file defines an exact tag for a component, **When** any compose file or Dockerfile uses that component through its variable, **Then** the image-pin check passes.
2. **Given** a compose file uses `latest`, no tag, a major-only or minor-only tag, or a literal tag not taken from the versions file, **When** the image-pin check runs, **Then** it fails and reports the file, line and offending reference.
3. **Given** a Dockerfile base image comes from a build argument, **When** that argument is not supplied, **Then** the build fails instead of falling back to a default version.
4. **Given** a developer or CI runner builds the repository, **When** the installed SDK differs from the pinned SDK version, **Then** the build fails with a clear message naming the required version.
5. **Given** the SDK image tag in the versions file differs from the pinned SDK version, **When** the image-pin check runs, **Then** it fails and names both versions.

---

### User Story 5 - Pull request gate for shared packages (Priority: P3)

When a pull request changes shared package sources or the pipeline itself, CI automatically runs the image-pin check, builds and packs the shared packages, and runs the consumer check. The PR shows pass or fail.

**Why this priority**: Every feature ships as its own PR, so PRs need an automatic gate. It is P3 because the local commands already work without it.

**Independent Test**: Open a PR that touches only the shared package folder: the workflow runs. Open a PR that touches only documentation: the workflow does not run.

**Acceptance Scenarios**:

1. **Given** a PR that changes files under the shared packages folder, **When** it is opened, **Then** the shared-packages workflow runs the image-pin check, pack and consumer steps.
2. **Given** a PR that changes no files under the workflow's paths, **When** it is opened, **Then** the workflow does not run.

### Edge Cases

- The pack command runs on a repository with no commits reachable from any matching tag, so the version falls back to the automatic prerelease scheme.
- The pack command runs on a shallow clone (as in CI). The version must still be computed correctly, or the command must fail clearly instead of producing a wrong version.
- The feed folder does not exist yet: the pack command creates it.
- The feed folder contains stale packages from an older build: older versions stay resolvable, and the newest pack does not overwrite a different build under the same version.
- A package project is added without a tag prefix: the build fails clearly instead of silently producing an unversioned package.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST provide the top-level layout defined by the constitution, created only as far as this feature needs: `src/building-blocks/`, `deploy/`, `artifacts/` (ignored by git, holding the feed at `artifacts/packages/`), `scripts/`, `tools/` and `.github/workflows/`. Other folders are created by the features that first need them.
- **FR-002**: The repository MUST provide one package configuration that resolves packages from the local feed `artifacts/packages/` and from the public package registry, with package source mapping so that shared packages (`BuildingBlocks.*`, `*.Contracts`) resolve **only** from the local feed.
- **FR-003**: The repository MUST provide baseline build settings inherited by every project: .NET 10 target, nullable enabled, implicit usings, warnings as errors, latest analyzers, deterministic builds.
- **FR-004**: Every packable project MUST get its version automatically from git tags, using a per-package tag prefix (`building-blocks/v*` for all BuildingBlocks packages, `<service>-contracts/v*` for each contracts package). Untagged builds MUST get an automatic prerelease version.
- **FR-005**: A packable project that declares no tag prefix MUST fail the build with an explanatory error.
- **FR-006**: A single pack command (`scripts/pack.ps1`, runnable on Windows and Linux with PowerShell 7) MUST build every packable project in the repository into `artifacts/packages/`, creating the folder if needed.
- **FR-007**: The repository MUST contain an initial packable project `BuildingBlocks.Core` under `src/building-blocks/` so the pipeline has a real package to produce. Its functional content is delivered by feature 002.
- **FR-008**: A consumer check command MUST create a temporary consumer project outside `src/`, pin a given package version, restore and build it against the feed, and always delete the temporary consumer when it exits.
- **FR-009**: Removed. Per the owner's decision, the no-comments rule is an authoring discipline (constitution IX), not an automated check.
- **FR-010**: A CI workflow for shared packages MUST run on pull requests and pushes that change `src/building-blocks/**`, `tools/**`, the pipeline scripts, the build configuration or `deploy/versions.env`. It runs the repository tool tests, the image-pin check, pack and the consumer check, with full git history so versions are computed correctly.
- **FR-011**: All scripts, configuration files and code introduced by this feature MUST contain no comments. This is an authoring rule and is checked in review, not by tooling.
- **FR-012**: The repository MUST provide `deploy/versions.env` as the single definition of container image versions, seeded with exact tags for the components Phase 1 introduces: .NET SDK build image, ASP.NET runtime image, PostgreSQL and RabbitMQ. Each value MUST be a full image reference whose tag is the component's full published release version plus OS variant, verified to exist in its registry when it is added.
- **FR-013**: An image-pin check command MUST scan every compose file, Dockerfile and test-fixture source. It MUST fail on any image reference that is `latest`, untagged, or a floating tag (major-only, or major.minor where the project publishes patch releases), or that is not taken from a `deploy/versions.env` variable or build argument. It reports file, line and reference. It MUST also fail if a referenced variable is not defined in `deploy/versions.env`, or if the same image repository is defined under more than one key.
- **FR-014**: The repository MUST pin the exact .NET SDK version for all builds (a `global.json` at the root, no roll-forward), CI MUST install exactly that SDK, and the SDK build image in `deploy/versions.env` MUST carry the same version.
- **FR-015**: The repository MUST provide a way for test code (Testcontainers fixtures) to read image tags from `deploy/versions.env`, so tests run the same versions as dev. The reader ships in a later BuildingBlocks testing package. This feature delivers the file format and the image-pin check coverage for test sources.

### Key Entities

- **Shared package**: a versioned, distributable unit of shared code (BuildingBlocks or a service's contracts). It has an identifier, a tag prefix and a version computed from git.
- **Local feed**: a folder holding produced packages. It is the only source for shared packages.
- **Tag prefix**: the per-package namespace for release tags. It determines which tags drive a package's version.
- **Image version entry**: a named, exact container image reference in `deploy/versions.env` (e.g. `POSTGRES_IMAGE`). It is the only allowed source of that component's version.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a clean checkout, one command produces every shared package with a correct version in under 2 minutes on a developer machine.
- **SC-002**: 100% of shared package versions come from git tags or the automatic prerelease scheme. Zero version numbers are written in project files.
- **SC-003**: A consumer outside the source tree restores and builds against a pinned package version on the first attempt, and leaves zero temporary files behind in every outcome, pass or fail.
- **SC-004**: Removed, together with FR-009.
- **SC-005**: A pull request touching shared package sources shows an automated pass/fail result without manual steps.
- **SC-006**: Every container component has exactly one version across dev, e2e, CI and tests: zero unpinned or duplicated image references in the repository.
- **SC-007**: The image-pin check detects 100% of the forbidden reference forms in a seeded test set.

## Assumptions

- This is the first of eight Phase 1 features (001 package-pipeline, 002 building-blocks-core, 003 building-blocks-permissions, 004 building-blocks-messaging, 005 dev-compose-infra, 006 identity-authentication, 007 identity-tenants-users, 008 api-gateway). Each ships as its own pull request.
- The Docker build-context pattern for packages (named build context for the feed) is delivered with the first Dockerfile in feature 006, not here.
- Moving the feed to a hosted registry is deferred to the deployment phase.
- PowerShell 7 is available on developer machines and CI runners (it is preinstalled on GitHub-hosted runners).
- The image-pin check is a repository tool (`tools/RepoChecks`) rather than a compiler analyzer, because it must read compose files, Dockerfiles and C# test sources.
- Code comments are prevented by authoring discipline. No comment-detection tooling is built.
- The CI workflow introduced here covers shared packages only. Per-service workflows arrive with each service.
