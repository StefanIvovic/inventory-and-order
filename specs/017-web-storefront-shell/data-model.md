# Data Model: Storefront Shell

Nothing is stored in the browser.

## Shop identifier

The first path segment after `/shop/`. It is valid when it matches `^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$`, the same rule as Identity's `Tenant.SlugPattern` and the gateway.

## Session view (from `GET /bff/shop/<slug>/user`, contract of feature 015)

| Field | Type | Rule |
|---|---|---|
| `userId` | string | required |
| `userType` | `"customer"` | the storefront only receives customers (Identity refuses others) |
| `tenantId` | string | not shown |
| `email` | string \| null | the account menu label and the account page; when null, the user id is shown |
| `logoutUrl` | string | `/bff/shop/<slug>/logout?sid=…`, used as is |
| `slug` | string | must equal the shop identifier in the URL (the gateway answers 401 otherwise) |

## Session state

| State | Data | Rendered as |
|---|---|---|
| `loading` | none | progress bar only |
| `no-shop` | none | "shop not found" page (no request is made) |
| `visitor` | `error`: sign-in error code or null | shop frame with "Sign in" and the error line if any |
| `unavailable` | none | shop frame with "Sign in" and a "The shop is having trouble right now" banner with "Try again" (repeats `load()`) |
| `signed-in` | `user`: Session view; `retryNeeded`: boolean | shop frame with the account menu. When `retryNeeded`, a "Sign-in service is unavailable" banner with "Try again" |

Transitions:
- **Start:**
  - an invalid slug gives `no-shop`
  - otherwise `loading`, then 200 gives `signed-in`, 401 gives `visitor`, and anything else gives `unavailable`
- **API answer 401:** `signed-in` becomes `visitor`.
- **API answer 503 `identity-unavailable`:** `retryNeeded = true` until "Try again".
