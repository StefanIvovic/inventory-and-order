# Data Model: BuildingBlocks Permissions

## PermissionDeniedException

| Field | Type | Rules |
|---|---|---|
| Missing | IReadOnlyList<string> | Non-empty. Missing names in the order requested, without duplicates |
| Code | string | Always `permission-denied` |

## Cache entry

| Field | Type | Rules |
|---|---|---|
| Key | string | `perm:{tenantId or empty}:{userId:N}` |
| Granted | IReadOnlySet<string> | Ordinal comparison |
| ExpiresAt | DateTimeOffset | `TimeProvider.GetUtcNow() + CacheDuration` when stored. Valid while `now < ExpiresAt` |

## PermissionOptions

| Field | Type | Rules |
|---|---|---|
| CacheDuration | TimeSpan | Default 60 seconds. Must be greater than zero, validated at startup |

## Permission resolution per request

| Caller | Result |
|---|---|
| Unauthenticated | Empty set. Neither the cache nor the provider is touched |
| Authenticated, cache valid | The cached set |
| Authenticated, cache missing or expired | The provider result, then stored. On failure: rethrow and store nothing |
