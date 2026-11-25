# Contract: Backoffice screens and navigation

## Screens

| State | Content |
|---|---|
| loading | Material progress bar at the top; nothing else |
| signed-out | Card titled "Backoffice", the text "Sign in to continue.", an error line if any, and a primary button "Sign in" |
| unavailable | Card with the text "The backoffice is unavailable. Try again in a moment." and a button "Try again" |
| signed-in | Toolbar with "Backoffice", a spacer, the user's email (or id) and role chip ("Staff" or "Platform administrator"), and a menu button with "Sign out". Below: `<router-outlet>` with the home placeholder "Welcome to the backoffice." |
| signed-in with `retryNeeded` | Above the outlet, a banner with "Sign-in service is unavailable." and "Try again" |

## Sign-in error messages

| `signin-error` | Message |
|---|---|
| `access_denied` | You don't have access to the backoffice. |
| `invalid_request` | The sign-in request was invalid. Please try again. |
| `server_error` | Sign-in is temporarily unavailable. Please try again. |
| any other | Sign-in failed. Please try again. |

## Navigation targets

| Action | Browser goes to |
|---|---|
| Sign in | `/bff/backoffice/login?returnUrl=<encoded current path and query without signin-error>` |
| Sign out | the session's `logoutUrl` |

## HTTP rules

| Request | Header added | On 401 | On 503 `identity-unavailable` |
|---|---|---|---|
| URL starts with `/api/` | `X-Bff-App: backoffice` | state becomes signed-out with the current path; the error is rethrown | banner shown; the request waits for "Try again" and is then re-sent |
| anything else | none | passed through | passed through |
