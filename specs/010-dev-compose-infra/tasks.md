---

description: "Task list for feature 010 Development Compose Infrastructure"
---

# Tasks: Development Compose Infrastructure

**Input**: Design documents from `specs/010-dev-compose-infra/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: These are CI checks (US4) plus one RepoChecks unit test. The spec asks for automated verification, so they are included.

**Global rules**:
- No comments in any file.
- No new images.
- Every host port binds to `127.0.0.1`.
- Tests never touch the dev stack.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Create `deploy/dev.env` with every key and value in data-model.md "Dev settings" (`RABBITMQ_USER`, `RABBITMQ_PASSWORD`, `RABBITMQ_AMQP_PORT`, `RABBITMQ_MANAGEMENT_PORT`, `IDENTITY_DB_PORT` … `REPORTING_DB_PORT`). No comments (research.md R5)

---

## Phase 2: Foundational

- [X] T002 Create `deploy/compose.yaml` containing only `name: inventory-and-order` and `include:`, with one entry `- path: infra/compose.yaml` that has `env_file: [versions.env, dev.env]` (data-model.md "Root compose file", research.md R1)

**Checkpoint**: `docker compose -f deploy/compose.yaml config --quiet` exits 0.

---

## Phase 3: User Story 1 - Start the dev infrastructure with one command (P1) 🎯 MVP

**Goal**: A healthy broker on loopback, started with one command, with data that persists.

**Independent Test**: quickstart.md sections 1 and 2.

- [X] T003 [US1] Create `deploy/infra/compose.yaml`, with service `rabbitmq` exactly as in research.md R4:
  - `image: ${RABBITMQ_IMAGE}`, profile `infra`, `hostname: rabbitmq`
  - `environment` mapped from `${RABBITMQ_USER}` and `${RABBITMQ_PASSWORD}`
  - ports `127.0.0.1:${RABBITMQ_AMQP_PORT}:5672` and `127.0.0.1:${RABBITMQ_MANAGEMENT_PORT}:15672`
  - named volume `rabbitmq-data` at `/var/lib/rabbitmq`
  - healthcheck `rabbitmq-diagnostics -q ping`, interval 5s, timeout 5s, retries 12, start_period 20s
  - a top-level `volumes: rabbitmq-data:` (data-model.md "Included compose file")
- [X] T004 [US1] Create `scripts/dev.ps1` per contracts/dev-script.md:
  - a `ValidateSet` positional `Command` (`up`, `down`, `reset`, `status`) and `-Profile` (default `infra`)
  - the compose file path resolved from `$PSScriptRoot`
  - a Docker reachability check that exits 2 with `Docker is not available.`
  - all four commands, exactly as in the contract table: `up` → `--profile <each -Profile> up --detach --wait --wait-timeout 120`, `down` → `--profile '*' down --remove-orphans`, `reset` → `--profile '*' down --volumes --remove-orphans`, `status` → `--profile '*' ps --all` (research.md R2, R3)
  - compose's exit code passed through
  - `Set-StrictMode` and `$ErrorActionPreference = 'Stop'`, as in `scripts/check-test-cleanup.ps1`
- [X] T005 [US1] Run quickstart.md sections 1 and 2 locally: healthy, UI login, loopback-only bindings, a queue that survives `down`/`up`. Record how long `up` takes from pulled images, against SC-001 (under 60s). Then run `./scripts/dev.ps1 reset`

**Checkpoint**: US1 works end to end on the dev machine.

---

## Phase 4: User Story 2 - Manage the dev stack's lifecycle safely (P2)

**Goal**: `status`, `down` and `reset` act only on the dev project.

**Independent Test**: quickstart.md section 3.

- [X] T006 [US2] Verify the error paths of `scripts/dev.ps1` locally: an unknown command exits 1 with the usage, and Docker stopped exits 2 with `Docker is not available.` (contracts/dev-script.md)
- [X] T007 [US2] Run quickstart.md section 3 locally: the canary survives `reset`, and no `inventory-and-order` volumes remain. Also run `reset` on a stopped stack: confirm exit code 0 and that any leftover dev volume is removed

**Checkpoint**: Reset is proven to be scoped to the dev project.

---

## Phase 5: User Story 3 - Later services plug in without changing the frame (P2)

**Goal**: The port convention is documented, and pin enforcement covers every compose file.

**Independent Test**: The README table matches `dev.env`, and the new RepoChecks test passes.

- [X] T008 [P] [US3] Create `deploy/README.md` with:
  - a statement that `dev.env` is for local development only
  - the host port table from data-model.md "Dev settings", pointing to `dev.env` as the source of truth
  - `dev.ps1` usage from contracts/dev-script.md
  - how a service adds one `include:` entry (research.md R6)
- [X] T009 [P] [US3] Add a test to `tools/RepoChecks.Tests/Images/ImageRulesTests.cs` that runs `ComposeRule.Check` on the real `deploy/infra/compose.yaml` against the real `deploy/versions.env`, and asserts there are no findings (research.md R9). Then run `dotnet test --solution tools/RepoChecks.slnx` and `dotnet run --project tools/RepoChecks -- images`
- [X] T009a [US3] Do the plug-in check from the spec's US3 Independent Test, in a scratch copy of `deploy/` outside the repo:
  - add a dummy `svc/compose.yaml` with `image: ${POSTGRES_IMAGE}`, profile `infra` and the port `127.0.0.1:${IDENTITY_DB_PORT}:5432`
  - add one `include:` entry for it to the copy's root file
  - confirm `docker compose --profile '*' config` resolves the pinned image and port 5401
  - delete the scratch copy and commit nothing

**Checkpoint**: US3 is documented and enforced.

---

## Phase 6: User Story 4 - CI proves the stack definition works (P3)

**Goal**: Every change to the deploy files is validated, smoke-tested through `dev.ps1`, and cleaned up.

**Independent Test**: The workflow passes on the PR and leaves no `inventory-and-order` or `canary` resources behind.

- [X] T010 [US4] Add `[string[]]$Project = @()` to `scripts/check-test-cleanup.ps1`. Each value appends `label=com.docker.compose.project=<value>` to `$filters`, and the default behaviour stays unchanged (contracts/dev-script.md, research.md R8)
- [X] T011 [US4] Create `.github/workflows/deploy.yml` per research.md R7 and R11:
  - triggers on `deploy/**`, `scripts/dev.ps1`, `scripts/check-test-cleanup.ps1` and the workflow file itself, for pull requests and pushes to master
  - `permissions: contents read`
  - `actions/checkout@v7.0.1` and `actions/setup-dotnet@v6.1.0` with `global-json-file`, the same pins as `building-blocks.yml`
  - steps: load `versions.env`, pull `RABBITMQ_IMAGE`, config validation (no profile and `'*'`), the image-pin check, start the canary container and volume with label `com.docker.compose.project=canary`, `dev.ps1 up`, `dev.ps1 status`
  - `if: always()` steps: `dev.ps1 reset`, assert the canary container is running and its volume exists, then remove both; `check-test-cleanup.ps1 -Project inventory-and-order`
- [X] T012 [US4] Validate locally:
  - run the T010 script with `-Project inventory-and-order` while the dev stack is down (passes) and while it is up (fails)
  - run the default check while the dev stack is up (passes)
  - temporarily break `deploy/infra/compose.yaml` (an invalid key), confirm that `docker compose -f deploy/compose.yaml --profile '*' config --quiet` exits non-zero, then revert (US4 scenario 2)
  - confirm the canary command `sleep 3600` keeps a container from `RABBITMQ_IMAGE` running
  - finish with `./scripts/dev.ps1 reset`

**Checkpoint**: CI is ready. It is proven on the PR run.

---

## Phase 7: User Story 5 - Constitution matches the test stack (P3)

**Goal**: Constitution v1.2.1 with no Respawn.

**Independent Test**: Read principle VII and the footer.

- [X] T013 [US5] Run `/speckit-constitution` to make a PATCH amendment to v1.2.1:
  - in principle VII's test stack line, remove "Respawn" and state that each integration test gets its own database
  - set **Last Amended** to 2026-11-24
  - change nothing else (research.md R10)

---

## Phase 8: Polish

- [X] T014 Run the full quickstart.md (sections 1–5) once more on a clean state. Confirm no comments were added to any new file. Finish with `./scripts/dev.ps1 reset` and `./scripts/check-test-cleanup.ps1`
- [ ] T015 Commit, push the branch, open the PR, and confirm that `deploy.yml` and `building-blocks.yml` both pass in CI

---

## Dependencies

- T001 → T002 → T003 → T004 → T005 (US1)
- US2 (T006, T007) needs T004
- US3: T008, T009 and T009a need T003; they are independent of each other
- US4: T010 is independent; T011 needs T004 and T010; T012 needs T010 and T004
- US5 (T013) is independent of everything
- Polish needs all of the above

## Parallel Opportunities

- T008 ∥ T009 ∥ T010 ∥ T013 once T003 is done (different files)

## Implementation Strategy

- MVP: Phases 1–3 (the broker up through `dev.ps1`).
- Then add US2 (lifecycle), US3 (docs and pin test), US4 (CI) and US5 (constitution).
- Everything ships in one PR, per the one-PR-per-feature rule.
