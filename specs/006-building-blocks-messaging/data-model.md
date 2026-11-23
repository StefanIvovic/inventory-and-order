# Data Model: BuildingBlocks Messaging

## MessagingSettings

| Field | Type | Rules |
|---|---|---|
| ServiceName | string | Required. Lowercase letters, digits and `-` only. Used as the queue name |
| PostgresConnectionString | string | Required. The service's own database |
| RabbitMqUri | Uri | Required. An `amqp://` URI |

## Envelope metadata

| Field | Source on outgoing | Read on incoming as |
|---|---|---|
| TenantId | The incoming envelope's TenantId, else the HTTP current user's tenant | `IEventContext.Tenant`, and `TenantContext` is set before the handler |
| `user-id` header | The incoming header, else the HTTP current user's id | `IEventContext.InitiatedByUserId` (GUID, or null) |
| CorrelationId | Wolverine's native correlation | `IEventContext.CorrelationId` |
| Id | Wolverine. On RabbitMQ it is `BasicProperties.MessageId` | Inbox deduplication key |

## Message identity

| Field | Rules |
|---|---|
| Identity | `^[a-z0-9-]+(\.[a-z0-9-]+)+\.v[0-9]+$`, for example `order.order-placed.v1`. Explicit through `[MessageIdentity]`. It is also the exchange name |

## Broker topology

| Element | Name | Type | Declared by |
|---|---|---|---|
| Exchange per event | identity | Fanout, durable | Publisher (`PublishEvent<T>`) and subscriber (`SubscribeToEvent<T>`) |
| Service queue | ServiceName | Durable | Subscriber (`ListenToServiceQueue`) |
| Binding | identity → ServiceName | — | Subscriber |
