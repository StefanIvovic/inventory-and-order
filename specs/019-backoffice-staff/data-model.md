# Data Model: Backoffice Staff

## StaffItem (from `GET /api/identity/users`)

| Field | Type | Rule |
|---|---|---|
| `id` | string (GUID) | used in action URLs |
| `email` | string | shown; the list is sorted by it, case-insensitive |
| `userType` | string | always `staff` here |
| `disabled` | boolean | shown as the name of the `staff-status` lookup value `disabled` or `active` |
| `roles` | string[] | shown as their `/roles` display names joined by ", ", or "No roles" when empty |
| `createdAt` | ISO date-time | shown as a local medium date |
| `actions.canDisable` | boolean | shows "Disable" only when true |
| `actions.canAssignRoles` | boolean | shows "Roles" only when true |

## Staff list request (`GET /api/identity/users`)

| Parameter | Type | Rule |
|---|---|---|
| `q` | string | optional; matches the start of the email, case-insensitive; at most 256 characters |
| `status` | `staff-status` lookup code: `active` \| `disabled` | optional; absent means all; anything else gives 400 |
| `role` | tenant role name | optional; one of the names in `GET /roles` |
| `size` | integer | 1–100, default 25 |
| `after` | opaque string | the previous page's `nextCursor`; malformed gives 422 `invalid-cursor` |
| `includeTotal` | boolean | default false; adds `total` |

## StaffList

| Field | Type | Rule |
|---|---|---|
| `items` | StaffItem[] | one page, sorted by email then id |
| `nextCursor` | string \| null | null on the last page |
| `total` | integer \| absent | present only with `includeTotal=true` |
| `actions.canCreate` | boolean | shows "Add staff user" only when true |

## NewStaffUser (body of `POST /api/identity/users`)

| Field | Label | Client checks | Sent as |
|---|---|---|---|
| `email` | Email | required; email format; at most 256 | trimmed |
| `password` | First password | required; at least 8 | as typed |

## Role (from `GET /api/identity/roles`)

| Field | Type | Rule |
|---|---|---|
| `name` | string | sent in role URLs and filters; never shown |
| `displayName` | string | shown |
| `permissions` | `{ name, displayName }[]` | `displayName` shown |

`canAssign` is not used on the Roles page, which is read-only.

## Tenant roles offered in the roles dialog and the role filter

Every role in the `GET /roles` answer, in its order. None is listed in the UI.
