# Data Model: Permission Invalidation

## PermissionAssignmentsChanged (event)

| Field | Type | Rules |
|---|---|---|
| TenantId | string? | The tenant whose assignment changed. Null for platform users |
| UserId | Guid | Required, not `Guid.Empty` |
| Identity | — | `building-blocks.permission-assignments-changed.v1` |

## Broker topology per service

| Element | Name | Properties |
|---|---|---|
| Exchange | `{ServiceName}.permission-assignments` | Fanout, durable |
| Instance queue | `{ServiceName}.permission-assignments.{instanceId}` | Non-durable, exclusive, auto-delete. One per process |
| Binding | exchange → instance queue | Created when the instance starts |

## Test-only: PermissionGrant (tenant-owned)

| Field | Type | Rules |
|---|---|---|
| Id | Guid | Key |
| UserId | Guid | — |
| Permission | string | — |
| TenantId | string | From `ITenantOwned` |
