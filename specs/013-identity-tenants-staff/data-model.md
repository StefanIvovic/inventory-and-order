# Data Model: Identity Tenants and Staff

## Tenant (new, `identity_tenants`)

| Field | Type | Rules |
|---|---|---|
| `Tenant.Id` | string(32) | Primary key. Generated GUID in `N` format. Never changes. It is the `tenant_id` everywhere |
| `Tenant.Slug` | string(63) | Required. Unique (`ux_identity_tenants_slug`). Regex `^[a-z0-9](?:[a-z0-9]\|-(?!-)){1,61}[a-z0-9]$`: 3–63 characters, lowercase letters, digits and single hyphens, no leading or trailing hyphen. Check constraint `ck_identity_tenants_slug`. Immutable in this feature |
| `Tenant.Name` | string(200) | Required, 1–200 characters after trimming |
| `Tenant.CreatedAt` | timestamptz | Set on creation |

Not tenant-owned (it is the tenant). No updates in this feature.

## AppUser (changed, `identity_users`)

| Field | Type | Rules |
|---|---|---|
| `AppUser.Email` | string(256) | Required. Valid email address. Normalized by `UserManager` |
| `AppUser.NormalizedEmail` | string(256) | Unique among non-customers: `ux_identity_users_staff_email` on `NormalizedEmail` `WHERE "UserType" <> 'customer'`. Replaces the global unique index from 012 |
| `AppUser.Password` (input only) | — | The 012 policy: at least 8 characters, ASP.NET Identity defaults otherwise |
| `AppUser.UserType` | string(20) | Unchanged: `staff`, `customer`, `platform`. This feature creates `staff` only |
| `AppUser.TenantId` | string(64) | Unchanged check constraint: required unless platform |
| `AppUser.DisabledAt` | timestamptz, nullable | New. Null means active. Set once by `Disable`; never cleared in this feature |
| `AppUser.CreatedAt` | timestamptz | New. Set on creation; existing rows get the migration time |

State transition: active → disabled (one way). Disabling also changes `SecurityStamp`.

## RoleAssignment (unchanged, `identity_role_assignments`)

Tenant-owned. Key tenant, user, role. `RoleAssignment.Role` must be a tenant role from `IdentityRoles` (`identity.administrator`, `identity.viewer`), max 100 characters.

## PlatformRoleAssignment (new, `identity_platform_role_assignments`)

| Field | Type | Rules |
|---|---|---|
| `PlatformRoleAssignment.UserId` | uuid | Part of the key. A platform user |
| `PlatformRoleAssignment.Role` | string(100) | Part of the key. A platform role (`identity.platform-administrator`) |
| `PlatformRoleAssignment.AssignedAt` | timestamptz | Set on creation |

Not tenant-owned. The migration inserts one row for every existing platform user.

## Roles and permissions (code)

| Role | Scope | Permissions |
|---|---|---|
| `identity.administrator` | tenant | `identity.users.view`, `identity.users.manage`, `identity.roles.view`, `identity.roles.assign` |
| `identity.viewer` | tenant | `identity.users.view`, `identity.roles.view` |
| `identity.platform-administrator` | platform | `identity.tenants.view`, `identity.tenants.manage` |

## Commands (validated after the permission check)

| Command | Fields and rules |
|---|---|
| `CreateTenant` | `Slug` (the `Tenant.Slug` rules), `Name` (the `Tenant.Name` rules), `AdminEmail` (the `AppUser.Email` rules), `AdminPassword` (the `AppUser.Password` policy) |
| `CreateStaffUser` | `Email` (the `AppUser.Email` rules), `Password` (the `AppUser.Password` policy) |
| `DisableUser` | `UserId` from the route |
| `AssignRole`, `UnassignRole` | `UserId` from the route, `Role` from the route: one of the tenant roles |

Password policy failures from `UserManager` are returned as 400 field errors on the password field, like validator failures.
