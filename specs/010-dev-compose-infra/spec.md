# Feature Specification: Development Compose Infrastructure

**Feature Branch**: `010-dev-compose-infra`

**Created**: 2026-11-24

**Status**: Draft

**Input**: User description: "010 dev-compose-infra. Development Docker Compose frame for the platform: a root compose file that only includes per-area compose files, a RabbitMQ broker in the `infra` profile, committed dev-only settings, loopback-only host ports with a fixed per-service Postgres port convention, a `scripts/dev.ps1` wrapper, a CI workflow that validates and smoke-starts the stack, and a patch amendment to the constitution. Observability is out of scope."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start the development infrastructure with one command (Priority: P1)

A developer with a fresh clone runs one command and gets a healthy message broker on their machine. They need no setup, no secrets to create and no extra flags. They can reach the broker and its management UI from their own machine only.

**Why this priority**: Every service feature from 011 onward needs a running broker and a place to plug its own database and API into. Without this frame, no service can run in development.

**Independent Test**: On a machine with Docker and no prior state, clone the repo, run the start command, and confirm the broker reports healthy and the management UI answers on the loopback address with the dev credentials.

**Acceptance Scenarios**:

1. **Given** a fresh clone and a running Docker engine, **When** the developer runs `scripts/dev.ps1 up`, **Then** the broker starts under the project name `inventory-and-order` and reports healthy before the command returns.
2. **Given** the stack is up, **When** the developer opens the management UI on `127.0.0.1:15672` and signs in with the credentials from `deploy/dev.env`, **Then** they see the broker overview.
3. **Given** the stack is up, **When** another machine on the same network tries the broker ports on the developer's LAN address, **Then** the connection is refused.
4. **Given** the stack was stopped with `scripts/dev.ps1 down`, **When** the developer runs `up` again, **Then** the broker state from before is still there.

---

### User Story 2 - Manage the development stack's lifecycle safely (Priority: P2)

A developer can check the status of the dev stack, stop it, and wipe it back to a clean state. None of these actions touch anything outside the dev project: test containers, end-to-end stacks and unrelated Docker projects stay as they are.

**Why this priority**: Day-to-day use needs stop, status and reset. Reset is destructive, so it must stay scoped to the dev project.

**Independent Test**: Start the dev stack and an unrelated compose project. Run `status`, `down` and `reset`, and confirm that only the dev project's containers and volumes change.

**Acceptance Scenarios**:

1. **Given** the stack is up, **When** the developer runs `scripts/dev.ps1 status`, **Then** they see each dev container with its state and health.
2. **Given** the stack is up, **When** the developer runs `scripts/dev.ps1 down`, **Then** the containers are removed and the named volumes are kept.
3. **Given** the stack is up and another compose project (for example `e2e-test`) is running, **When** the developer runs `scripts/dev.ps1 reset`, **Then** the dev project's containers and volumes are removed and the other project is untouched.
4. **Given** an unknown command or a stopped Docker engine, **When** the developer runs the script, **Then** it prints a clear message and exits with a non-zero code.

---

### User Story 3 - Later services plug into the frame without changing it (Priority: P2)

A developer adding the first service (feature 011) adds that service's own compose file, with its API and database, and plugs it in with one `include:` entry in the root file. The host port for the service's database is already decided by the documented convention, and the service gets the same pinned image versions and dev settings as everything else.

**Why this priority**: The frame exists so services can be added without reworking the infrastructure. It is the condition for 011 to 013.

**Independent Test**: In a scratch check, add a dummy compose file under a service-like folder with a `${POSTGRES_IMAGE}` reference and one `include:` line, and confirm the full stack configuration resolves with the pinned image and the convention port. The dummy is not committed.

**Acceptance Scenarios**:

1. **Given** the root file, **When** a reviewer reads it, **Then** it contains only `include:` entries, each loading both `versions.env` and `dev.env`, and no services of its own.
2. **Given** the port convention in the deployment documentation, **When** a service feature needs a database host port, **Then** it finds a fixed, unique number for that service.
3. **Given** an included compose file that references an image other than through a `deploy/versions.env` variable, **When** the image-pin check runs, **Then** it fails and names the file and line.

---

### User Story 4 - CI proves the stack definition works (Priority: P3)

A change to the deployment files or the dev script triggers a CI workflow. It validates the configuration of every profile and checks image pins. It then runs the stack through `scripts/dev.ps1` exactly as a developer would (`up`, `status`, `reset`) and verifies that nothing is left behind.

**Why this priority**: It stops broken or unpinned compose changes, and a broken dev script, from reaching master. It matters less than the stack itself.

**Independent Test**: Open a pull request that changes only `deploy/`, and confirm the workflow runs, passes, and leaves no containers or volumes from the dev project. A deliberately broken compose file must make it fail.

**Acceptance Scenarios**:

1. **Given** a change under `deploy/**` or to `scripts/dev.ps1`, **When** it is pushed, **Then** the deployment workflow runs. A change elsewhere does not trigger it.
2. **Given** an invalid compose file, **When** the workflow runs, **Then** the configuration step fails for the affected profile.
3. **Given** the smoke start fails or times out, **When** the workflow ends, **Then** `scripts/dev.ps1 reset` still runs and the cleanup check still verifies that the dev project left nothing behind.
4. **Given** a container from another compose project is running before `reset`, **When** the workflow runs `scripts/dev.ps1 reset`, **Then** that container is still running afterwards.

---

### User Story 5 - Constitution matches the actual test stack (Priority: P3)

The constitution's test stack lists only tools the project uses. Respawn is removed, because each test gets its own database.

**Why this priority**: It's a small documentation fix, but the constitution is the source of truth for every plan's Constitution Check.

**Independent Test**: Read the amended constitution and confirm the version, the amendment date and the test stack line.

**Acceptance Scenarios**:

1. **Given** the amended constitution, **When** a reviewer reads principle VII, **Then** the test stack does not mention Respawn and says that integration tests use one database per test.
2. **Given** the amended constitution, **When** a reviewer reads its footer, **Then** the version is 1.2.1 and the last-amended date is the amendment date.

---

### Edge Cases

- Ports 5672 or 15672 are already used on the host (another broker, a previous stack under a different name): `up` exits non-zero with Docker's port-in-use error. A container may be left in `Created` state, and `reset` removes it.
- A developer runs plain `docker compose up` from `deploy/` without the script: the root file's `name:` and the per-include env files still give the same project name, images and settings.
- Docker Desktop is not running: the script reports that the engine is unreachable instead of printing a raw stack trace.
- `reset` is run when the stack is not up: it completes without error and removes any leftover dev volumes.
- Testcontainers runs and the e2e project run while the dev stack is up: random ports and different project names mean no conflict, and the dev broker is never reached by tests.
- The CI smoke run is cancelled midway: the always-run `reset` still removes the dev project from the runner.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repo MUST have a root compose file at `deploy/compose.yaml` that sets the project name `inventory-and-order` and contains only `include:` entries. It defines no services, networks or volumes of its own.
- **FR-002**: Every `include:` entry MUST load `deploy/versions.env` and `deploy/dev.env`, so image references and dev settings resolve without command-line flags or an untracked `.env` file.
- **FR-003**: The repo MUST have `deploy/infra/compose.yaml`, which defines one RabbitMQ broker with the management UI. It sits in profile `infra`, takes its image only from `${RABBITMQ_IMAGE}`, and has a healthcheck based on the broker's own ping diagnostic.
- **FR-004**: The broker MUST keep its data in a named volume, so state survives `down` and `up`.
- **FR-005**: The repo MUST commit `deploy/dev.env`, holding the dev broker credentials and host ports. The values must be plainly dev-only, so the password itself says it is not a secret. The deployment README must say the file is never used outside local development. The file contains no comments.
- **FR-006**: Every host port that the dev stack publishes MUST be bound to `127.0.0.1`. The broker publishes AMQP on 5672 and the management UI on 15672.
- **FR-007**: The deployment documentation MUST set down a fixed host port for each service's development database: Identity 5401, Product 5402, Inventory 5403, Order 5404 and Payment 5405. Later services continue the sequence (Shipping 5406, Notification 5407, Reporting 5408).
- **FR-008**: The broker MUST NOT be configured with a topology definitions file. Queues and exchanges are provisioned by the services' messaging setup.
- **FR-009**: The repo MUST provide `scripts/dev.ps1` with the commands `up`, `down`, `reset` and `status`. They act only on the dev project defined by `deploy/compose.yaml`, with every profile that exists in this feature (`infra`).
- **FR-010**: `up` MUST wait until every started container is healthy, or fail with a non-zero exit code.
- **FR-011**: `reset` MUST remove the dev project's containers, named volumes and orphans, and MUST NOT affect any other compose project or Testcontainers resources.
- **FR-012**: The script MUST exit non-zero with a readable message for an unknown command, a missing Docker engine or a failed compose call.
- **FR-013**: A new CI workflow `deploy.yml` MUST run on changes to `deploy/**`, `scripts/dev.ps1`, `scripts/check-test-cleanup.ps1` (which it depends on) and the workflow file itself, and on nothing else.
- **FR-014**: The workflow MUST validate the merged compose configuration of the root file for every profile it defines.
- **FR-015**: The workflow MUST run the existing image-pin check, which MUST cover compose files under `deploy/` and any `src/**/compose.yaml`.
- **FR-016**: The workflow MUST smoke-test the stack through `scripts/dev.ps1` itself: `up` (which waits for health), then `status`, then `reset` in an always-run step. The CI runner has no dev stack, so using the dev project `inventory-and-order` there is safe. Before `reset`, the workflow MUST start a canary container in a separate compose project, and after `reset` it MUST verify that the canary is still running, then remove it.
- **FR-017**: After `reset`, the workflow MUST run the test cleanup check against the dev project and verify that no containers or volumes labelled `com.docker.compose.project=inventory-and-order` remain. The check MUST take the extra project as an optional parameter. Its default behaviour (Testcontainers and `e2e-test` only) stays unchanged, so running it locally never flags a developer's running dev stack.
- **FR-018**: Tests MUST NOT use the dev stack. The e2e project keeps the name `e2e-test` with only anonymous volumes or `tmpfs`, and Testcontainers keeps random ports.
- **FR-019**: The constitution MUST be amended to version 1.2.1 (patch). Remove Respawn from the test stack in principle VII and state that integration tests use one database per test. Update the version and the last-amended date. The constitution has no sync impact report.
- **FR-020**: Observability components (collector, metrics, traces, logs, dashboards) MUST NOT be added in this feature. The `observability` profile name stays reserved for the phase-5 feature.

### Key Entities

- **Dev project**: the compose project `inventory-and-order`. It owns the dev containers and named volumes and is the only target of `scripts/dev.ps1`.
- **Profile**: a named group of containers (`infra` here; `apps`, `observability` and `cache` reserved). It decides what each command starts.
- **Dev settings file**: `deploy/dev.env`, the committed dev-only credentials and ports. It is loaded with `versions.env` for every include.
- **Port convention**: the documented table of fixed loopback host ports, one per service database plus the broker ports.
- **Canary project**: a one-container compose project that CI starts before `reset` to prove that `reset` touches only the dev project.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a fresh clone with images already pulled, one command brings the dev infrastructure to healthy in under 60 seconds.
- **SC-002**: Starting the dev stack needs zero manual setup steps beyond having Docker running: no files to create, no secrets to copy, no flags to remember.
- **SC-003**: 0 dev stack ports are reachable from any address other than the developer's own machine.
- **SC-004**: After `reset`, 0 containers and 0 volumes of the dev project remain, and 100% of other projects' containers and volumes are unchanged.
- **SC-005**: After every CI run of the deployment workflow, pass, fail or cancel, 0 containers and 0 volumes of the dev project remain on the runner, and the canary container survives `reset`.
- **SC-006**: Adding a new service to the dev stack needs exactly one new line group in the root file and no changes to existing included files.
- **SC-007**: 100% of image references in compose files resolve through the single versions file, as enforced by CI.

## Assumptions

- Developers use Docker Desktop on Windows (or Docker Engine elsewhere) with a Compose version that supports `include:` with per-include `env_file` (verified on v5.5.1; GitHub runners ship a current Compose), and PowerShell 7+.
- The broker image and version are already pinned in `deploy/versions.env` (`RABBITMQ_IMAGE`). No new image is added in this feature.
- The existing image-pin check already finds compose files anywhere outside excluded folders. If verification shows a gap for `deploy/` or `src/**/compose.yaml`, the check is extended in this feature.
- The codegen wording in the constitution already names feature 011 (it was corrected earlier), so the amendment only covers the Respawn line.
- Dev-only credentials in a committed file are acceptable because the stack binds only to loopback and is never deployed. Production secrets are handled in phase 7.
- Per-service Postgres containers, service APIs, the gateway and the SPAs come with their own features (011 onward) and are not part of this feature.
- The CI smoke run uses GitHub-hosted Linux runners, which have no dev stack, so the fixed host ports do not clash there.
