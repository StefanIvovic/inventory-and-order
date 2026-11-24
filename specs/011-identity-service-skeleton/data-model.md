# Data Model: Identity Service Skeleton

## IdentityPermissions (code, not stored)
| Constant | Value |
|---|---|
| `RolesView` | `identity.roles.view` |
| `RolesAssign` | `identity.roles.assign` (defined but granted by no role in 011) |

## IdentityRoles (code, not stored)
| Name | Permissions |
|---|---|
| `identity.administrator` | `identity.roles.view` |

Rules:
- Role names and permission strings are stable, lowercase and dot-separated.
- Every permission a role grants must exist in `IdentityPermissions`. A unit test checks this.

## RoleAssignment (table `role_assignments`)
| Field | Type | Rules |
|---|---|---|
| `TenantId` | text | required. Tenant-owned (`ITenantOwned`), so the BuildingBlocks tenant filter applies |
| `UserId` | uuid | required |
| `Role` | text | required, max 100, must be a name in `IdentityRoles` (enforced by a check constraint in the migration) |
| `AssignedAt` | timestamptz | required, set from `TimeProvider` |

- Primary key: (`TenantId`, `UserId`, `Role`).
- Lookups by (`TenantId`, `UserId`) use the primary key prefix. The BuildingBlocks tenant setup also adds an index on `TenantId`.
- Platform-level assignments (no tenant) are out of scope until 013.

## Permission resolution (`IdentityPermissionProvider : IPermissionProvider`)
- The permissions are the union of `IdentityRoles[role].Permissions` over the caller's `RoleAssignment` rows for the current tenant.
- A platform caller gets the empty set in 011.

## GetRoles → RolesResponse
```
GetRoles()                                  (no fields)
RolesResponse { Roles: RoleDto[], CanAssign: bool }
RoleDto       { Name: string, Permissions: string[] }    (sorted by name)
```

## Route category (endpoint metadata)
| Category | Marker | Who passes authentication and authorization |
|---|---|---|
| Tenant (default) | none | a valid token, a well-formed identity with a tenant, and `user_type` not `platform` |
| Platform | `PlatformOnly` | a valid token, `user_type=platform`, no tenant |
| Allow-listed | `AllowAnonymous` | anyone |

Both markers on one endpoint is a startup error.

## Messaging storage
Wolverine's outbox, inbox and node tables in schema `wolverine`, created by Wolverine on startup.
