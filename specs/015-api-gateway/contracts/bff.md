# Contract: BFF endpoints

All paths are on the gateway origin. `{prefix}` is `/bff/backoffice` for the backoffice, or `/bff/shop/{slug}` for the storefront, where `{slug}` must match Identity's slug pattern (otherwise 404).

## Login

`GET {prefix}/login?returnUrl=<path>`

- `302` to Identity's `/connect/authorize` with `client_id` (`backoffice-bff` or `storefront-bff`), `response_type=code`, `response_mode=form_post`, `code_challenge_method=S256`, `scope=openid offline_access email identity product inventory order payment`, `state`, `nonce`, and for the storefront `tenant=<slug>`.
- `returnUrl` is used only if it is a local path under `/backoffice/` (or `/shop/{slug}/`). Otherwise the app's start page.
- **Callback** (`POST /bff/backoffice/signin-oidc`, `POST /bff/storefront/signin-oidc`): sets the app cookie, then `302` to the return path.
- **Failure**: `302` to `<app start>?signin-error=<access_denied|invalid_request|server_error>`, with no cookie.

## User

`GET {prefix}/user`

- `200`:
  ```json
  {
    "userId": "6f1c…",
    "userType": "staff",
    "tenantId": "0b9e…",
    "email": "admin@demo.local",
    "logoutUrl": "/bff/backoffice/logout?sid=Zr4…"
  }
  ```
  `tenantId` is `null` for the platform admin. The storefront adds `"slug": "demo"`.
- `401` `session-required`: no session, or a storefront session for another slug. Never redirects.

## Logout

`GET {prefix}/logout?sid=<value from logoutUrl>`

- With a session and the matching `sid`: clears the cookie, then `302` to Identity's `/connect/endsession` with `id_token_hint`, `post_logout_redirect_uri=<origin>/bff/<backoffice|storefront>/signout-callback-oidc` and `state`. The callback then `302`s to the app's start page (`/backoffice/` or `/shop/{slug}/`).
- Without a session: `302` to the app's start page.
- With a session and a missing or wrong `sid`: `400` `logout-proof-required`, and the session stays.

## API calls

`<any method> /api/<service>/<path>` with the header `X-Bff-App: backoffice` or `X-Bff-App: storefront`

| Condition | Answer |
|---|---|
| `Sec-Fetch-Site` present and not `same-origin` | `403` `cross-site-request` |
| header missing or another value | `400` `app-header-required` |
| no session for that app | `401` `session-required` |
| refresh refused by Identity | `401` `session-ended`, and that app's cookie is cleared |
| Identity unreachable during refresh | `503` `identity-unavailable`, and the session stays |
| upstream unreachable | `502` (or `504`) `upstream-unavailable` |
| otherwise | the service's response, unchanged, possibly with an updated app cookie |

## Problem details

Every gateway-made error is `application/problem+json` with `type` (the slug above), `title`, `status` and `traceId`, the same shape as BuildingBlocks problems.
