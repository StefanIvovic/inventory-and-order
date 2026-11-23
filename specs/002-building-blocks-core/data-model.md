# Data Model: BuildingBlocks Core

## CurrentUser (immutable)

| Field | Type | Rules |
|---|---|---|
| UserId | Guid | From `sub`. It must parse as a GUID, otherwise 401 |
| TenantId | string? | From `tenant_id`. Required unless `UserType` is Platform, otherwise 401. When present, it is non-empty |
| UserType | UserType | From `user_type`, parsed case-insensitively. It must be one of `Staff`, `Customer`, `Platform`, otherwise 401 |

`ICurrentUser` adds `IsAuthenticated`. Reading `UserId` or `UserType` while unauthenticated
throws `InvalidOperationException`. `TenantId` returns null.

## UserType (enum)

`Staff`, `Customer`, `Platform`

## TenantContext (scoped, set once)

| Field | Type | Rules |
|---|---|---|
| TenantId | string? | Null until set |
| HasTenant | bool | `TenantId is not null` |

State transitions: `Unset → Set(x)`. `Set(x)` again → no-op. `Set(y ≠ x)` → `InvalidOperationException`. `Set(null or whitespace)` → `ArgumentException`.

## DomainException

| Field | Type | Rules |
|---|---|---|
| Code | string | Required, not null or whitespace, otherwise `ArgumentException` at construction |
| Kind | DomainErrorKind | `Conflict` (→ 409) or `Rule` (→ 422) |
| Message | string | Becomes `detail` |

## NotFoundException

| Field | Type | Rules |
|---|---|---|
| Code | string | Always `not-found` |
| Resource | string | Required, used in the generic detail |
| Message | string | Fixed: `The requested {Resource} was not found.` |

## AppClaimTypes (constants)

`Subject = "sub"`, `TenantId = "tenant_id"`, `UserType = "user_type"`
