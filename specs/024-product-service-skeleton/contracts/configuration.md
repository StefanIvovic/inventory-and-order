# Contract: Configuration

## Product API (compose `product-api`)

| Setting | Dev value | Rule |
|---|---|---|
| `ConnectionStrings__Product` | `Host=product-db;Port=5432;Database=product;Username=${PRODUCT_DB_USER};Password=${PRODUCT_DB_PASSWORD};GSS Encryption Mode=Disable` | required |
| `RabbitMq__Uri` | `amqp://${RABBITMQ_USER}:${RABBITMQ_PASSWORD}@rabbitmq:5672` | required |
| `Authentication__Authority` | `${IDENTITY_ISSUER}` | required; the public issuer |
| `Authentication__Audience` | `product` | required |
| `Authentication__RequireHttpsMetadata` | `${IDENTITY_REQUIRE_HTTPS}` | `false` in dev |
| `Authentication__InternalAddress` | `http://identity-api:8080` | new; optional in general, required inside Docker |

## `deploy/dev.env` additions

| Name | Value |
|---|---|
| `PRODUCT_DB_USER` | `product` |
| `PRODUCT_DB_PASSWORD` | a dev-only password, like Identity's |

`PRODUCT_DB_PORT=5402` already exists.

## Gateway

| Setting | Value |
|---|---|
| `ReverseProxy:Routes:api-product` | `Match.Path /api/product/{**rest}`, `Metadata.Session required`, `PathRemovePrefix /api/product`, cluster `product` |
| `ReverseProxy__Clusters__product__Destinations__main__Address` | `http://product-api:8080` |

## `codegen.env` (Product)

Placeholder `Authentication__Authority` and `Authentication__Audience`, so `ValidateOnStart` passes during `codegen write`, and `ASPNETCORE_ENVIRONMENT=Development`.
