# Data Model: Backoffice Shell

Nothing is stored in the browser. All state lives in memory and comes from the gateway.

## Session view (from `GET /bff/backoffice/user`, contract of feature 015)

| Field | Type | Rule |
|---|---|---|
| `userId` | string | required |
| `userType` | `"staff"` \| `"platform"` | shown as "Staff" or "Platform administrator". The backoffice never receives `"customer"` (Identity refuses it) |
| `tenantId` | string \| null | `null` for the platform admin; not shown yet |
| `email` | string \| null | shown in the toolbar; when null, the user id is shown instead |
| `logoutUrl` | string | local path beginning `/bff/backoffice/logout?sid=`; used as is, never built by the app |

## Session state (signal in `SessionService`)

| State | Data | Rendered as |
|---|---|---|
| `loading` | none | progress bar only |
| `signed-out` | `error`: sign-in error code or null | sign-in screen. "Sign in" sends the browser's current path and query, without `signin-error`, as the return path |
| `unavailable` | none | "The backoffice is unavailable" with "Try again" (repeats `load()`) |
| `signed-in` | `user`: Session view; `retryNeeded`: boolean | frame. When `retryNeeded`, a banner "Sign-in service is unavailable" with "Try again" |

Transitions:
- **Start:** `loading` → `signed-in` on 200, `signed-out` on 401, `unavailable` on any other answer or a network error.
- **API answer 401:** `signed-in` → `signed-out`. The user is still on the same page, so signing in returns there.
- **API answer 503 `identity-unavailable`:** `signed-in` stays signed in with `retryNeeded = true`. "Try again" sets it back to `false` and re-sends the waiting requests.
- **Sign in / sign out:** these are full-page navigations, so the state is rebuilt after the browser returns.

## Sign-in error

Read from `?signin-error=` once at start, then removed from the address bar. The codes and messages are in contracts/ui.md.
