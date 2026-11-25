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
| Gateway (the only application port) | `GATEWAY_PORT` | 8080 |

RabbitMQ management UI: http://127.0.0.1:15672, user `dev`, password `dev-only-not-a-secret`.

## Identity sign-in in development

Identity issues tokens for the issuer `IDENTITY_ISSUER` (the gateway origin, `http://localhost:8080/`). Identity publishes no port; it is reached through the gateway, and it trusts forwarded headers only from the private container networks (`IDENTITY_FORWARDED_NETWORK_*`) and only for the host `GATEWAY_HOST`. Its signing and encryption certificates are created on first start in the `identity-keys` volume, so tokens survive restarts. `dev.ps1 reset` removes them.

The platform admin is created on first start from `PLATFORM_ADMIN_EMAIL` and `PLATFORM_ADMIN_PASSWORD`. Sign in through the gateway at http://localhost:8080/bff/backoffice/login. HTTPS is not required in dev (`IDENTITY_REQUIRE_HTTPS=false`). Production keeps the default, which requires HTTPS.

## Web apps and live reload

The backoffice (`src/web/backoffice`) and the storefront (`src/web/storefront`) are served by nginx as `backoffice-spa` and `storefront-spa` on port 8080 inside the compose network. The gateway serves them at http://localhost:8080/backoffice/ and http://localhost:8080/shop/<slug>/. `./scripts/dev.ps1 up -Profile infra,apps -WebDev` adds `compose.web-dev.yaml`, which runs both Angular dev servers (`backoffice-dev`, `storefront-dev`) and points the gateway at them. Use the same `-WebDev` with `down` and `reset`.

## End-to-end tests

`./scripts/e2e.ps1` starts a copy of the stack in the compose project `e2e-test` with `tests/e2e/compose.e2e.yaml`: no published ports, `tmpfs` instead of volumes. It runs Playwright in the gateway's network namespace, so the browser uses `http://localhost:8080` exactly as in development. It always ends with `down -v --remove-orphans` and the cleanup check, so it can run while the dev stack is up.

## Adding a service

A service keeps its API and database in its own `src/services/<name>/compose.yaml`, with the database port bound as `127.0.0.1:${<NAME>_DB_PORT}:5432`. Plug it in with one entry in `compose.yaml`:

```yaml
  - path: ../src/services/<name>/compose.yaml
    env_file:
      - versions.env
      - dev.env
```

Images come only from `versions.env` as `${KEY}`. The image-pin check in CI enforces this.
