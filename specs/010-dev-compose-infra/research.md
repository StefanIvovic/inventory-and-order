# Research: Development Compose Infrastructure

## R1. Loading env files for each include

- **Decision**: The root `deploy/compose.yaml` uses the long `include:` form, `- path: infra/compose.yaml` with `env_file: [versions.env, dev.env]`. The paths are relative to `deploy/`.
- **Rationale**: Each included file is interpolated from the env files of its include, so `${RABBITMQ_IMAGE}` resolves with no `--env-file` flag and no `.env` file. This was checked locally on Compose v5.5.1: the image and the loopback port resolved in `config`.
- **Alternatives considered**:
  - `deploy/.env`: it is git-ignored and is read only from the project directory.
  - `--env-file` in the script: plain `docker compose` would then break.

## R2. Profiles and lifecycle commands

- **Decision**: `up` starts the profiles passed in `-Profile` (default `infra`) with `up --detach --wait --wait-timeout 120`. `down`, `reset` and `status` always use `--profile '*'`.
- **Rationale**: Compose acts only on services in active profiles. If `down` or `reset` ran without the profiles, a service started under some profile could be left running. `'*'` turns on every profile the project defines, so teardown is complete.
- **Alternatives considered**: Listing the profiles in the script. This needs editing every time a profile is added.

## R3. What `reset` removes

- **Decision**: `docker compose -f deploy/compose.yaml --profile '*' down --volumes --remove-orphans`.
- **Rationale**: Compose scopes this to the project `inventory-and-order`. It removes the project's containers, network and declared named volumes, and nothing outside the project. CI proves this with a canary (R7).
- **Alternatives considered**: `docker volume prune` or a label sweep. Both reach beyond the project.

## R4. Broker container

- **Decision**:
  - Image `${RABBITMQ_IMAGE}` (already pinned: `rabbitmq:4.4.1-management-alpine`).
  - `environment: RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}` and `RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}`.
  - Ports `127.0.0.1:${RABBITMQ_AMQP_PORT}:5672` and `127.0.0.1:${RABBITMQ_MANAGEMENT_PORT}:15672`.
  - Named volume `rabbitmq-data` at `/var/lib/rabbitmq`.
  - `hostname: rabbitmq`, so the broker's node name, and with it its stored data, stays the same across restarts.
  - Healthcheck: `rabbitmq-diagnostics -q ping`, interval 5s, timeout 5s, retries 12, start_period 20s.
- **Rationale**:
  - Env files only feed interpolation, so the credentials have to be mapped into `environment:` explicitly.
  - RabbitMQ stores its data per node name, and the node name comes from the hostname. A random container hostname would lose the data on every restart.
  - `ping` is RabbitMQ's documented lightweight liveness check.
- **Alternatives considered**:
  - `check_port_connectivity` or `status`: both are slower and heavier.
  - A definitions file: excluded by FR-008.

## R5. dev.env contents

- **Decision**: `RABBITMQ_USER=dev`, `RABBITMQ_PASSWORD=dev-only-not-a-secret`, `RABBITMQ_AMQP_PORT=5672`, `RABBITMQ_MANAGEMENT_PORT=15672`, and the service database ports `IDENTITY_DB_PORT=5401` through `REPORTING_DB_PORT=5408`. No comments in the file. The dev-only notice lives in `deploy/README.md` and in the password value itself.
- **Rationale**: This makes the file the single source of the port convention. Service features from 011 on reference `${IDENTITY_DB_PORT}` and do not add new numbers. Principle IX: no comments in any file. The user is `dev` rather than `guest`, because RabbitMQ's `guest` account only accepts connections from localhost inside the container.
- **Alternatives considered**: Ports listed only in the README. The convention would then exist in two places.

## R6. Port convention documentation

- **Decision**: `deploy/README.md` has a short table of every dev host port and points to `dev.env` as the source of truth. It also lists the `dev.ps1` commands.
- **Rationale**: FR-007 requires documentation. A single README next to the files is where developers look first.

## R7. CI smoke test through dev.ps1

- **Decision**: A new workflow, `.github/workflows/deploy.yml`, on `ubuntu-latest`, with these steps:
  1. Pull `RABBITMQ_IMAGE`.
  2. Run `docker compose config --quiet` for the root file, once with no profile and once with `--profile '*'`.
  3. Run `dotnet run --project tools/RepoChecks -- images`.
  4. Start the canary: `docker run -d --name canary --label com.docker.compose.project=canary -v canary-data:/data "$RABBITMQ_IMAGE" sleep 3600`, with the volume labelled the same way.
  5. Run `./scripts/dev.ps1 up`, then `./scripts/dev.ps1 status`.
  6. `if: always()`: run `./scripts/dev.ps1 reset`.
  7. `if: always()`: check that the canary container is still running and its volume still exists, then remove both.
  8. `if: always()`: run `./scripts/check-test-cleanup.ps1 -Project inventory-and-order`.
- **Rationale**:
  - The canary reuses the already pulled and pinned broker image, so no new image enters `versions.env`.
  - A label-only fake project proves that `reset` doesn't touch containers or volumes outside its own project.
  - pwsh is preinstalled on GitHub's Ubuntu runners, and so is a recent Compose that supports include `env_file`.
- **Alternatives considered**:
  - A canary compose file in the repo: an extra file, and the pin check would need to see it.
  - A `busybox` canary: a new image to pin.

## R8. Extending the cleanup check

- **Decision**: Add `[string[]]$Project = @()` to `scripts/check-test-cleanup.ps1`. Each value adds a filter `label=com.docker.compose.project=<value>` to the existing two. The default is unchanged.
- **Rationale**: This meets FR-017 while keeping the local runs that use the default from flagging a developer's dev stack.

## R9. Image-pin check coverage

- **Decision**: No change to RepoChecks.
- **Rationale**: `SourceWalker` matches `^(docker-)?compose([.-][\w.-]+)?\.ya?ml$` across the repo, and only `bin`, `obj`, `.git`, `.specify`, `.claude`, `artifacts`, `specs` and `node_modules` are excluded. So `deploy/compose.yaml`, `deploy/infra/compose.yaml` and any future `src/**/compose.yaml` are already checked. `ComposeRule` skips the root file, which has no `services:`, and checks the broker's `image: ${RABBITMQ_IMAGE}`. A RepoChecks test is added that feeds the real `deploy/infra/compose.yaml` through the rule, which proves the coverage.

## R10. Constitution amendment

- **Decision**: Run `/speckit-constitution` (governance requires it) to make the change to v1.2.1, PATCH. In principle VII, the test stack line drops "Respawn" and states that each integration test gets its own database. The last-amended date becomes 2026-11-24.
- **Rationale**: The codegen line already says feature 011 (commit 62fab1d), so the Respawn line is the only remaining drift. The constitution has no sync impact report, so the version bump and the date are the record.

## R11. Workflow triggers

- **Decision**: `deploy.yml` runs on pull requests and on pushes to master, for these paths: `deploy/**`, `scripts/dev.ps1`, `scripts/check-test-cleanup.ps1` and `.github/workflows/deploy.yml`.
- **Rationale**: FR-013. The cleanup script is added because the workflow depends on its new parameter. `building-blocks.yml` already triggers on `scripts/**` and `deploy/versions.env`, so that workflow stays as it is.
