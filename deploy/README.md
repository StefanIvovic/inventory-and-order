# Development stack

`compose.yaml` is the root of the local development stack. It contains only `include:` entries, and each entry loads `versions.env` (pinned images) and `dev.env` (dev settings).

`dev.env` is for local development only. Its credentials are not secrets and must never be used anywhere else. Every port binds to `127.0.0.1`.

## Commands

| Command | Effect |
|---|---|
| `./scripts/dev.ps1 up` | Start the `infra` profile and wait until healthy |
| `./scripts/dev.ps1 up -Profile infra,apps` | Also build and start the services (run `./scripts/restore-feed.ps1 -Service <name>` first) |
| `./scripts/dev.ps1 status` | Show containers, state and health |
| `./scripts/dev.ps1 down` | Stop and remove containers, keep data |
| `./scripts/dev.ps1 reset` | Remove the containers and data of this project only |

The compose project is `inventory-and-order`. Plain `docker compose -f deploy/compose.yaml --profile infra up` works too.

## Host ports

`dev.env` is the source of truth. Services use its variables and never hard-code a port.

| What | Variable | Port |
|---|---|---|
| RabbitMQ AMQP | `RABBITMQ_AMQP_PORT` | 5672 |
| RabbitMQ management UI | `RABBITMQ_MANAGEMENT_PORT` | 15672 |
| Identity database | `IDENTITY_DB_PORT` | 5401 |
| Product database | `PRODUCT_DB_PORT` | 5402 |
| Inventory database | `INVENTORY_DB_PORT` | 5403 |
| Order database | `ORDER_DB_PORT` | 5404 |
| Payment database | `PAYMENT_DB_PORT` | 5405 |
| Shipping database | `SHIPPING_DB_PORT` | 5406 |
| Notification database | `NOTIFICATION_DB_PORT` | 5407 |
| Reporting database | `REPORTING_DB_PORT` | 5408 |
| Identity API (temporary, until the gateway in feature 014) | `IDENTITY_API_PORT` | 5101 |

RabbitMQ management UI: http://127.0.0.1:15672, user `dev`, password `dev-only-not-a-secret`.

## Adding a service

A service keeps its API and database in its own `src/services/<name>/compose.yaml`, with the database port bound as `127.0.0.1:${<NAME>_DB_PORT}:5432`. Plug it in with one entry in `compose.yaml`:

```yaml
  - path: ../src/services/<name>/compose.yaml
    env_file:
      - versions.env
      - dev.env
```

Images come only from `versions.env` as `${KEY}`. The image-pin check in CI enforces this.
