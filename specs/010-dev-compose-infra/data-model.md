# Data Model: Development Compose Infrastructure

This feature has no application data. The "entities" below are the configuration units and the rules they follow.

## Dev project
- Compose project `inventory-and-order`, set by `name:` in `deploy/compose.yaml`.
- It owns the broker container, the network `inventory-and-order_default` and the volume `inventory-and-order_rabbitmq-data`.
- It is the only target of `scripts/dev.ps1`.

## Root compose file (`deploy/compose.yaml`)
- Keys: `name` and `include` only.
- Every include entry has `path` and `env_file: [versions.env, dev.env]`.
- Adding a service means adding one include entry (SC-006).

## Included compose file (`deploy/infra/compose.yaml`)
- Service `rabbitmq`, profile `infra`, settings as in research R4.
- Volume `rabbitmq-data`, named and local.

## Dev settings (`deploy/dev.env`)
| Key | Value | Used by |
|---|---|---|
| `RABBITMQ_USER` | `dev` | broker |
| `RABBITMQ_PASSWORD` | `dev-only-not-a-secret` | broker |
| `RABBITMQ_AMQP_PORT` | `5672` | broker |
| `RABBITMQ_MANAGEMENT_PORT` | `15672` | broker |
| `IDENTITY_DB_PORT` | `5401` | feature 011 |
| `PRODUCT_DB_PORT` | `5402` | later |
| `INVENTORY_DB_PORT` | `5403` | later |
| `ORDER_DB_PORT` | `5404` | later |
| `PAYMENT_DB_PORT` | `5405` | later |
| `SHIPPING_DB_PORT` | `5406` | later |
| `NOTIFICATION_DB_PORT` | `5407` | later |
| `REPORTING_DB_PORT` | `5408` | later |

Rules:
- Every published port binds to `127.0.0.1`.
- Each port number is unique.
- There are no real secrets.

## Profiles
- `infra` is used now.
- `apps`, `observability` and `cache` are reserved and not defined in this feature.

## Canary (CI only)
- The container `canary` and the volume `canary-data` are both labelled `com.docker.compose.project=canary`.
- They must survive `reset`, and they are removed afterwards.
