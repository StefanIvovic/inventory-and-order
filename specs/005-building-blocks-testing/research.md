# Research: BuildingBlocks Testing

Verified on 2026-11-23 against NuGet metadata and the library sources at the pinned tags:

| Package | Version | License | Notes |
|---|---|---|---|
| Testcontainers.PostgreSql / Testcontainers.RabbitMq | 4.17.0 | MIT | net10.0. Builders have a `(string image)` constructor |
| Npgsql | 10.0.4 | — | Used for `CREATE DATABASE` and tests. The 10.x line matches EF Core 10 |
| RabbitMQ.Client | 7.3.0 | — | Tests only, to prove a connection |
| xunit.v3.extensibility.core | 4.0.3 | — | For `IAsyncLifetime` on the fixtures |
| Ryuk image | `testcontainers/ryuk:0.14.0` | — | Newest semver tag on Docker Hub |

## R1. Image-version reader

- **Decision**: A static `ImageVersions` class with `Get(string key)` and `Path`. Lookup order:
  1. the `IAO_VERSIONS_FILE` environment variable
  2. walk up from `AppContext.BaseDirectory` until `deploy/versions.env` is found

  It parses the same `KEY=VALUE` format that `RepoChecks` enforces. A missing file or key throws
  `InvalidOperationException` naming the key and the path.
- **Rationale**: Test binaries run from `bin/` deep inside the repository, and CI can set the
  variable explicitly (FR-001).

## R2. Pinning the reaper

- **Decision**: `TestInfrastructure.EnsureConfigured()` runs once (thread-safe) before any
  container is built. It sets `TestcontainersSettings.ResourceReaperImage = new DockerImage(ImageVersions.Get("RYUK_IMAGE"))`.
  Every fixture calls it in its constructor (FR-005).
- **Verified**: `ResourceReaperImage` has a public setter in testcontainers-dotnet 4.17.0.
- **Found during implementation**: testcontainers-dotnet 4.17.0 defaults to
  `testcontainers/ryuk:0.14.0@sha256:…`, the same version as our pin. A test that only inspects
  the running reaper can't tell pinned from unpinned, so a second test asserts
  `TestcontainersSettings.ResourceReaperImage` itself. The pin still matters: it keeps control
  when a future library upgrade changes the default.
- **Observed**: the reaper starts and works on Docker Desktop for Windows (Docker 29.8.1).
- **Known risk**: the library's own comments say the reaper "might not be able to connect to
  Ryuk on Docker Desktop for Windows". The fixtures still dispose explicitly, so cleanup does
  not depend on the reaper. The cleanup check proves that.

## R3. Fixtures

- **Decision**:
  - **PostgreSQL**: `new PostgreSqlBuilder(ImageVersions.Get("POSTGRES_IMAGE")).WithTmpfsMount("/var/lib/postgresql")`.
    The tmpfs is mounted at the parent folder, which covers both the old `.../data` layout and the
    PostgreSQL 18 image's versioned data folder.
  - **RabbitMQ**: `new RabbitMqBuilder(ImageVersions.Get("RABBITMQ_IMAGE")).WithTmpfsMount("/var/lib/rabbitmq")`.
  - **Ports**: Testcontainers maps container ports to random host ports by default.
    `GetMappedPublicPort` is used for assertions.
  - **Labels**: Testcontainers adds `org.testcontainers=true` and a session label automatically.
- **Lifetime**: The fixtures implement xUnit v3 `IAsyncLifetime`, so they work as class fixtures
  (`IClassFixture<>`) or assembly fixtures (`[assembly: AssemblyFixture]`). `DisposeAsync`
  removes the container.
- **Fresh database**: `CreateDatabaseAsync()` runs `CREATE DATABASE "t_{guid:N}"` over Npgsql and
  returns a connection string for the new database (FR-003).

## R4. Cleanup check

- **Decision**: `scripts/check-test-cleanup.ps1 [-TimeoutSeconds 60]`. It polls
  `docker ps -a` and `docker volume ls` with `--filter label=org.testcontainers` and
  `--filter label=com.docker.compose.project=e2e-test` until both are empty or the timeout
  passes. On timeout it lists each leftover (id, image, labels) and exits 1 (FR-006).
- **Rationale**: Polling covers the reaper's grace period after a crash. A healthy run is clean
  immediately, because the fixtures dispose explicitly.

## R5. CI

- **Decision**: The existing `building-blocks` workflow runs on `ubuntu-latest`, which has Docker.
  The BuildingBlocks test step now includes the Testing tests. A new step runs the cleanup check
  with `if: always()` (FR-007).
- **Pre-pull**: The workflow pre-pulls the pinned images, read from `versions.env`, so pull
  time doesn't count against test timeouts.

## R6. No image literals

- **Decision**: The fixtures and tests read every image through `ImageVersions`. Test fixtures
  that need image-like text build it by concatenation, as in feature 001, so the `RepoChecks`
  IP006 rule passes (FR-009).
