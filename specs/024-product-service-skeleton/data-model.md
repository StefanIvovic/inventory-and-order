# Data Model: Product Service Skeleton

## RoleAssignment (table `role_assignments`, Product database)

| Field | Type | Rule |
|---|---|---|
| `TenantId` | `varchar(64)` | required; length, stamping and the global tenant filter come from `TenantDbContext` |
| `UserId` | `uuid` | required |
| `Role` | `varchar(100)` | required; one of `ProductRoles.Tenant` (check constraint) |
| `AssignedAt` | `timestamptz` | required |

Key: `(TenantId, UserId, Role)`. Empty in this feature; written from 025.

## ProductPermissions (code)

| Name | Display name |
|---|---|
| `product.roles.view` | View roles |

## ProductRoles (code)

| Name | Display name | Permissions |
|---|---|---|
| `product.administrator` | Administrator | every Product permission |
| `product.viewer` | Viewer | `product.roles.view` |

## GetRoles answer

| Field | Type | Rule |
|---|---|---|
| `Roles[].Name` | string | role name |
| `Roles[].DisplayName` | string | from `ProductRoles.DisplayNames` |
| `Roles[].Permissions[]` | `{ name, displayName }` | sorted by display name |
| `CanAssign` | bool | always `false` in this feature |

Roles are sorted by display name.

## AuthenticationSettings (BuildingBlocks.AspNetCore 0.5.0)

| Field | Type | Rule |
|---|---|---|
| `Authority` | string | required; the public issuer |
| `Audience` | string | required |
| `RequireHttpsMetadata` | bool | default `true` |
| `InternalAddress` | string? | new, optional; an absolute `http` or `https` URI with no path; when set, backchannel requests to the authority's origin go here with forwarded headers |
