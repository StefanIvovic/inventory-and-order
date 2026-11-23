# Feature Specification: BuildingBlocks Testing

**Feature Branch**: `005-building-blocks-testing`

**Created**: 2026-11-23

**Status**: Draft

**Input**: User description: "Feature 005 of Phase 1. A shared testing package that every service's integration tests use: read container image versions from deploy/versions.env (promised in feature 001, FR-015), throwaway PostgreSQL and RabbitMQ containers with random ports and in-memory storage, the test-container cleanup reaper pinned like every other image, and an automated check that no test containers or volumes are left behind. This prepares feature 006 (Wolverine messaging), the first feature that needs real infrastructure in tests."

## User Scenarios & Testing *(mandatory)*

The users are developers writing integration tests, and the CI pipeline.

### User Story 1 - Tests run the same image versions as dev (Priority: P1)

A test asks for "the PostgreSQL image" or "the RabbitMQ image" and gets exactly the reference pinned in `deploy/versions.env`. A test can never pick a different version by accident.

**Why this priority**: Constitution VIII requires the same component to run the same version in dev, e2e, CI and tests, with tests reading the tag from `deploy/versions.env`.

**Independent Test**: Read each key and compare it with the file. A missing key or a missing file fails with a clear message.

**Acceptance Scenarios**:

1. **Given** `deploy/versions.env` defines `POSTGRES_IMAGE`, **When** a test asks for it, **Then** it gets exactly that value.
2. **Given** a key that the file does not define, **When** a test asks for it, **Then** it fails with a message naming the key and the file.
3. **Given** a test running from any build output folder inside the repository, **When** the versions file is looked up, **Then** it is found by walking up to the repository root.
4. **Given** the versions file location is provided explicitly (for example in CI), **When** it is looked up, **Then** that location is used.

---

### User Story 2 - Throwaway PostgreSQL and RabbitMQ for integration tests (Priority: P1)

An integration test starts a PostgreSQL or RabbitMQ container from the shared package, gets a ready connection string, and needs no setup code. Each container uses the pinned image, a random host port, and in-memory storage. It never touches the dev environment and leaves nothing on disk.

**Why this priority**: Every service's integration tests and feature 006 depend on this. Without it, each service would reinvent fixtures and isolation would drift.

**Independent Test**: Start each container, connect and run a trivial operation, check the image reference and the port, dispose, and confirm the container is gone.

**Acceptance Scenarios**:

1. **Given** a PostgreSQL fixture, **When** it starts, **Then** a connection with its connection string can run a query, and the container image equals `POSTGRES_IMAGE`.
2. **Given** a RabbitMQ fixture, **When** it starts, **Then** a client can open a connection and a channel with its connection string, and the container image equals `RABBITMQ_IMAGE`.
3. **Given** a running fixture, **When** its host port is inspected, **Then** it is a random port, never the default 5432 or 5672, so it cannot collide with a dev stack running at the same time.
4. **Given** a running fixture, **When** its storage is inspected, **Then** data lives in memory only and no named volume is attached.
5. **Given** a PostgreSQL fixture, **When** a test asks for a fresh, empty database, **Then** it gets a new database with a unique name on the same container, so tests in one run do not see each other's data.
6. **Given** a fixture, **When** it is disposed, **Then** its container is stopped and removed.

---

### User Story 3 - Nothing is left behind (Priority: P1)

After any test run, passing or failing, no test containers or volumes remain. A check verifies it automatically, locally and in CI.

**Why this priority**: Constitution VII requires that, and an automated check that verifies it.

**Independent Test**: Run the test suite, then the check. It passes. Start a labelled container manually and run the check. It fails and names the container.

**Acceptance Scenarios**:

1. **Given** a completed test run, **When** the cleanup check runs, **Then** it passes, because no containers or volumes carry the test-container label or the `e2e-test` compose project label.
2. **Given** a leftover container or volume with one of those labels, **When** the cleanup check runs, **Then** it fails and lists each leftover.
3. **Given** the test process crashed or was killed, **When** the cleanup reaper finishes its grace period, **Then** the leftovers are removed. The check waits a bounded time for the reaper before it fails.
4. **Given** CI, **When** the test step fails, **Then** the cleanup check still runs.

---

### User Story 4 - The cleanup reaper image is pinned too (Priority: P2)

The helper container that removes leftovers after a crash uses a version pinned in `deploy/versions.env`, like every other image, rather than whatever the library defaults to.

**Why this priority**: Constitution VIII says no unpinned images anywhere. It is P2 because the reaper does not affect test results.

**Independent Test**: Start a fixture and inspect the reaper container's image. It equals the pinned value.

**Acceptance Scenarios**:

1. **Given** `deploy/versions.env` defines the reaper image, **When** the first fixture starts, **Then** the reaper container runs exactly that image.
2. **Given** the image-pin check from feature 001, **When** it runs, **Then** the reaper entry passes the same exact-tag rules as every other entry.

### Edge Cases

- Docker is not running. Fixtures fail fast with a message saying Docker is required, rather than hanging.
- Two test projects run in parallel. Each gets its own containers on random ports.
- The image is not present locally. It is pulled once and reused on later runs.
- A test fails in the middle. The fixture is still disposed.
- The developer's dev stack (feature 009) is running at the same time. There are no port, name or volume conflicts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The testing package MUST provide an image-version reader for `deploy/versions.env`. It locates the file by walking up from the test's base folder, or from an explicitly configured path, and fails clearly for a missing file or key.
- **FR-002**: The testing package MUST provide a PostgreSQL fixture and a RabbitMQ fixture usable as shared per-test-class or per-assembly fixtures. Each uses the pinned image, a random host port, in-memory data storage, no named volumes, and test-container labels.
- **FR-003**: The PostgreSQL fixture MUST offer creating a fresh database with a unique name and returning its connection string.
- **FR-004**: Fixtures MUST stop and remove their containers on disposal. The cleanup reaper MUST stay enabled as a safety net.
- **FR-005**: `deploy/versions.env` MUST gain a pinned entry for the cleanup reaper image, and the fixtures MUST make the reaper use it.
- **FR-006**: A cleanup check command MUST fail if any container or volume carries the test-container label or the `e2e-test` compose project label. It waits a bounded time (at most 60 seconds) for the reaper, and lists every leftover.
- **FR-007**: CI MUST run the integration tests on a Docker-capable runner, and MUST run the cleanup check even when tests fail.
- **FR-008**: The testing package MUST be packable under the `building-blocks/v` prefix, so services consume it from the feed like every other shared package.
- **FR-009**: Source code using the fixtures MUST NOT contain image literals. The existing image-pin check rule for test sources applies.

### Key Entities

- **Image versions**: the pinned image references read from `deploy/versions.env`.
- **Test container**: a throwaway, labelled, randomly-ported, in-memory instance of an infrastructure component.
- **Cleanup reaper**: the helper container that removes test containers if a test process dies.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of containers started by fixtures use exactly the image pinned in `deploy/versions.env`, the reaper included.
- **SC-002**: After every test run, locally and in CI, the cleanup check finds zero leftover containers and zero leftover volumes.
- **SC-003**: An integration test needs at most 2 lines to get a ready database or broker connection string.
- **SC-004**: The integration tests can run while a dev stack is up, with zero conflicts.

## Assumptions

- Reaper cleanup after a crashed or killed test process (US3 scenario 3) is verified manually, because killing a process inside an automated test is fragile. Automated coverage is the reaper image test plus the cleanup check, which waits for the reaper.
- Docker is available on developer machines (Docker Desktop) and on GitHub-hosted Ubuntu runners.
- A unique database per test (rather than a database-reset tool) gives isolation inside one container. A reset tool can be added later if needed.
- The cleanup check is a PowerShell 7 script, like the other repository scripts, so it runs on Windows and Linux.
- Phase 1 roadmap after this change:
  - 005 testing
  - 006 messaging
  - 007 permission-invalidation
  - 008 validation
  - 009 dev-compose-infra
  - 010 identity-authentication
  - 011 identity-tenants-users
  - 012 api-gateway
