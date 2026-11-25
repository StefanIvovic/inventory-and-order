# Data Model: Backoffice Tenants

## TenantItem (from `GET /api/identity/tenants`)

| Field | Type | Rule |
|---|---|---|
| `id` | string | not shown |
| `slug` | string | shown as "Identifier" |
| `name` | string | shown as "Name" |
| `createdAt` | ISO date-time string | shown as "Created", local medium date and time; the list is sorted by it, newest first |

## TenantList

| Field | Type | Rule |
|---|---|---|
| `items` | TenantItem[] | may be empty ("No tenants yet.") |
| `actions.canCreate` | boolean | shows "Create tenant" only when true |

## NewTenant (body of `POST /api/identity/tenants`)

| Field | UI label | Client checks | Sent as |
|---|---|---|---|
| `slug` | Identifier | required; `^[a-z0-9](?:[a-z0-9]\|-(?!-)){1,61}[a-z0-9]$` | trimmed, lower-cased |
| `name` | Name | required; at most 200 characters | trimmed |
| `adminEmail` | First admin email | required; email format; at most 256 characters | trimmed |
| `adminPassword` | First admin password | required; at least 8 characters | as typed |

These checks mirror Identity's `CreateTenantValidator`, which stays authoritative: its 400 field errors are shown on the same fields.

## Page state

`loading` → `loaded { items, canCreate }` or `error`. "Try again" goes back to `loading`. A user type other than `platform` gives `not-available` without loading.
