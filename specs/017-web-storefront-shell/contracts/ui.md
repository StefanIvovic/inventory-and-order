# Contract: Storefront screens, routes and navigation

## Routes (base `/shop/`)

| Path | Page |
|---|---|
| `/shop/<slug>/` and any unknown path under it | Home: "Welcome to <slug>." |
| `/shop/<slug>/account` | Account |
| `/shop/` and `/shop/<anything not matching the rule>/...` | Shop not found: "This shop doesn't exist." |

## Frame

- **Toolbar**: the shop identifier as the title, then a spacer, then one of:
  - visitor or unavailable: a "Sign in" button
  - signed in: a menu button labelled with the email (or user id), with `aria-label="Account menu"`. The menu holds "Account", which links to `/shop/<slug>/account`, and "Sign out".
- **Banners**, above the content:
  - the sign-in error line (`role="alert"`)
  - "The shop is having trouble right now." with "Try again" (`unavailable`)
  - "Sign-in service is unavailable." with "Try again" (`retryNeeded`)

## Account page

| State | Content |
|---|---|
| signed in | heading "Your account", then "Email: <email>" and "Shop: <slug>" |
| otherwise | "Sign in to see your account." and a "Sign in" button |

## Sign-in error messages

| Code | Message |
|---|---|
| `access_denied` | This account can't be used in this shop. |
| `invalid_request` | This shop isn't available for sign-in. |
| `server_error` | Sign-in is temporarily unavailable. Please try again. |
| any other | Sign-in failed. Please try again. |

## Navigation targets

| Action | Browser goes to |
|---|---|
| Sign in | `/bff/shop/<slug>/login?returnUrl=<encoded current path and query without signin-error>` |
| Sign out | the session's `logoutUrl` |

## HTTP rules

| Request | Header | 401 | 503 `identity-unavailable` |
|---|---|---|---|
| URL starts with `/api/` | `X-Bff-App: storefront` | state becomes `visitor`; the error is rethrown | banner; the request waits for "Try again" and is then re-sent |
| anything else | none | passed through | passed through |
