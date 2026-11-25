# Events: Identity Tenants and Staff

Package `Identity.Contracts` 0.1.0 (tag `identity-contracts/v0.1.0`), namespace `Identity.Contracts`, no dependencies. Message identity: the full type name. Each is published to its own fanout exchange named by that identity (`PublishEvent<T>`). Headers: `tenant-id` (the affected tenant), `user-id` (the initiating user; absent for the demo seed), `correlation-id`.

| Event | Fields | Sent when |
|---|---|---|
| `TenantCreated` | `string TenantId`, `Guid AdminUserId` | a tenant and its first admin are created |
| `UserRegistered` | `Guid UserId`, `string TenantId`, `string UserType`, `string Email` | any tenant user is created, including a tenant's first admin |
| `UserDisabled` | `Guid UserId`, `string TenantId` | an active user becomes disabled (not on a repeated disable) |

Delivery: transactional outbox, at least once. Consumers must be idempotent. A rolled-back change sends nothing.

`PermissionAssignmentsChanged` (BuildingBlocks) is also sent on role changes and disabling, to Identity's own instances only.
