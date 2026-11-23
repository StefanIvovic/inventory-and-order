# Data Model: BuildingBlocks Testing

## versions.env entries (after this feature)

| Key | Value | Used by |
|---|---|---|
| DOTNET_SDK_IMAGE | `mcr.microsoft.com/dotnet/sdk:10.0.401-alpine3.24` | Dockerfiles (later) |
| DOTNET_ASPNET_IMAGE | `mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24` | Dockerfiles (later) |
| POSTGRES_IMAGE | `postgres:18.7-alpine3.24` | `PostgresFixture`, compose (later) |
| RABBITMQ_IMAGE | `rabbitmq:4.4.1-management-alpine` | `RabbitMqFixture`, compose (later) |
| RYUK_IMAGE | `testcontainers/ryuk:0.14.0` | `TestInfrastructure` (reaper) |

## ImageVersions lookup

| Step | Rule |
|---|---|
| 1 | If `IAO_VERSIONS_FILE` is set, use it. If that file doesn't exist, fail |
| 2 | Otherwise walk up from `AppContext.BaseDirectory` and use the first `deploy/versions.env` found |
| 3 | If none is found, fail with the start folder in the message |
| Key | Must be present. Otherwise fail with the key name and the file path |

## Test container

| Field | Rule |
|---|---|
| Image | Exactly the `versions.env` value |
| Host port | Random. Never the default (5432 for PostgreSQL, 5672 for RabbitMQ) |
| Storage | tmpfs (`/var/lib/postgresql`, `/var/lib/rabbitmq`). No volumes |
| Labels | `org.testcontainers=true`, plus the session id |
| Lifetime | Started in `InitializeAsync`, removed in `DisposeAsync` |

## Cleanup check result

| State | Exit code |
|---|---|
| No container or volume carries `org.testcontainers` or `com.docker.compose.project=e2e-test` within the timeout | 0 |
| Leftovers remain after the timeout | 1, and each one is listed |
| Docker is unavailable | 2 |
