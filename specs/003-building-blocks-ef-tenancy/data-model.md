# Data Model: BuildingBlocks EF Tenancy

## ITenantOwned (marker, in Core)

| Field | Type | Rules |
|---|---|---|
| TenantId | string | Read-only for the domain. Required. Max length 64. Indexed. Set once: stamped on insert when empty, and immutable afterwards |

## Tenant filter

| Field | Value | Rules |
|---|---|---|
| Name | `"Tenant"` | Used for `IgnoreQueryFilters` |
| Predicate | `e.TenantId == CurrentTenantId` | Fails closed when `CurrentTenantId` is null |

## Write-guard decisions per entry

| Entry state | Context tenant | Entity TenantId | Outcome |
|---|---|---|---|
| Added | none | any | Reject |
| Added | `t` | empty | Stamp `t` |
| Added | `t` | `t` | Accept |
| Added | `t` | `u ≠ t` | Reject |
| Modified | any | property modified | Reject |
| Modified | any | unchanged | Accept |
| Deleted | any | any | Accept (the row could only be loaded under the filter) |

Rejecting throws `TenantViolationException` before any database command, so the save writes
nothing.
