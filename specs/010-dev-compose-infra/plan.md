# Implementation Plan: Development Compose Infrastructure

**Branch**: `010-dev-compose-infra` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/010-dev-compose-infra/spec.md`

## Summary

This feature adds the dev compose frame:
- a root `deploy/compose.yaml` that only includes other files, each loading `versions.env` and `dev.env`
- `deploy/infra/compose.yaml` with one RabbitMQ broker: healthy, bound to loopback only, data in a named volume
- a committed `deploy/dev.env` that holds the credentials and the `54xx` port convention
- `deploy/README.md` with the port table and the commands
- `scripts/dev.ps1` with `up`, `down`, `reset` and `status`

A new `deploy.yml` workflow does the following:
- validates the config
- runs the existing image-pin check
- smoke-tests through `dev.ps1`
- proves with a canary that `reset` is scoped to the dev project
- runs the cleanup check, which gets a new `-Project` parameter

A patch amendment to the constitution (v1.2.1) removes Respawn from the test stack.

## Technical Context

**Language/Version**: Compose YAML, PowerShell 7, GitHub Actions YAML. One C# test is added in RepoChecks.Tests (.NET 10)

**Primary Dependencies**: Docker Compose with include `env_file` support (verified on v5.5.1 locally). `RABBITMQ_IMAGE` is already pinned

**Storage**: One named Docker volume for the broker (dev only)

**Testing**: CI config validation, the image-pin check, a smoke test through `dev.ps1`, a canary for `reset` scope, the cleanup check, and one RepoChecks unit test on the real infra compose file

**Target Platform**: Windows with Docker Desktop (dev), and the `ubuntu-latest` runner (CI)

**Project Type**: Infrastructure configuration and scripts

**Performance Goals**: Healthy within 60s from pulled images (SC-001). The `up` wait timeout is 120s

**Constraints**:
- loopback-only ports
- no new images
- no comments in any file (the dev-only notice is in the README; see research R5)
- tests never touch dev

**Scale/Scope**: About 9 files: 3 compose/env files, a README, the script, the workflow, an edit to the cleanup script, a test, and the constitution

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Per-service compose files come with each service; the root only includes them |
| VII. Tests isolated | ✅ | CI only: the runner has no dev stack. Testcontainers and e2e are unchanged. The default cleanup check is unchanged locally |
| VIII. Everything in Docker, pinned | ✅ | The broker uses `${RABBITMQ_IMAGE}`. The canary reuses it. The pin check covers every compose file (R9) |
| IX. No comments | ✅ | No comments in compose, env, script or workflow files |
| Workflow gates | ✅ | The pin check and the cleanup check run in the new workflow |
| Governance | ✅ | The amendment goes through `/speckit-constitution` as a PATCH (R10) |

**Gate: PASS**, both before and after design.

## Project Structure

### Documentation (this feature)

```text
specs/010-dev-compose-infra/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/dev-script.md
├── checklists/requirements.md
└── tasks.md            (next: /speckit-tasks)
```

### Source Code (repository root)

```text
deploy/
├── compose.yaml          new: name + include only
├── infra/compose.yaml    new: rabbitmq, profile infra
├── dev.env               new: dev credentials + ports
├── README.md             new: port table, dev.ps1 usage
└── versions.env          unchanged
scripts/
├── dev.ps1               new
└── check-test-cleanup.ps1  edit: -Project parameter
tools/RepoChecks.Tests/   new test: real deploy/infra/compose.yaml passes ComposeRule
.github/workflows/deploy.yml   new
.specify/memory/constitution.md   v1.2.1: Respawn removed
```

**Structure Decision**: Everything lives in the existing `deploy/`, `scripts/` and `.github/workflows/` folders, as constitution B15/D7 requires. No new projects.

## Complexity Tracking

No deviations.
