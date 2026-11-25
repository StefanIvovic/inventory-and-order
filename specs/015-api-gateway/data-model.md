# Data Model: API Gateway

The gateway stores nothing in a database. Its state lives in the encrypted session cookie and in process memory.

## App session (authentication ticket in the app's cookie)

| Field | Source | Notes |
|---|---|---|
| `sub` | identity token | user id |
| `user_type` | identity token (new in Identity, R4) | `staff`, `platform` or `customer` |
| `tenant_id` | identity token (new) | absent for the platform admin |
| `email` | identity token (new, `email` scope) | |
| `.Token.access_token` | token response | sent as the bearer token, never to the browser |
| `.Token.refresh_token` | token response | rotated on every refresh |
| `.Token.id_token` | token response | `id_token_hint` at sign-out |
| `.Token.expires_at` | token response | drives the refresh (threshold 1 minute) |
| `bff_sid` | gateway, at sign-in | 32 random bytes, base64url; refresh lock key and sign-out proof |
| `slug` | storefront login request | storefront only; the shop the session is for |

- Cookie names: `bff-backoffice` and `bff-storefront`, prefixed with `__Host-Http-` when `PublicOrigin` is `https` (research.md R2). The ticket lifetime is 12 hours, with no sliding renewal. Each refresh sets a new 12-hour expiry (research.md R8). The cookie itself is a browser-session cookie.
- **Lifecycle**: created at the sign-in callback. Rewritten when a refresh changes the tokens. Deleted at sign-out, at a refused refresh, or when its ticket expires. Replaced by a new sign-in of the same app (storefront: any shop).

## Refresh state (memory, singleton)

| Item | Key | Lifetime |
|---|---|---|
| Per-session lock | `bff_sid` | removed when no request holds or waits for it |
| Refresh result (new tokens, or "refused") | `bff_sid` + SHA-256 of the refresh token that was redeemed | 5 minutes |
| Revoked session (signed out, or refresh refused) | `bff_sid` | until the ticket's own expiry (research.md R14) |

## Configuration (`Gateway` section)

| Key | Example (dev) | Rule |
|---|---|---|
| `PublicOrigin` | `http://localhost:8080` | absolute, no path; equals Identity's issuer without the trailing slash; its scheme decides the `__Host-` prefix and `RequireHttpsMetadata` |
| `Identity:InternalAddress` | `http://identity-api:8080` | absolute |
| `Clients:backoffice-bff:Secret`, `Clients:storefront-bff:Secret` | from `dev.env` | required |
| `Refresh:Threshold` | `00:01:00` | positive, under the access-token lifetime |
| `Keys:Path` | `/keys` | required outside Development and Testing |
| `ReverseProxy:Clusters:<name>:Destinations:main:Address` | `http://identity-api:8080` | one per cluster: `identity`, `backoffice-spa`, `storefront-spa` |

Validated at startup (`ValidateOnStart`). An invalid value stops the gateway.

## Identity changes

- **Scope** `email`: no resource. Permission granted to both gateway clients.
- **Identity-token claims**: `sub` (unchanged); `user_type` and `tenant_id` (also in the access token, unchanged there); `email`, only with the `email` scope.
- **Settings** `Identity:ForwardedHeaders:KnownNetworks` (CIDR list; empty means no forwarded header is trusted) and `Identity:ForwardedHeaders:AllowedHosts` (host list; required whenever networks are set; dev `localhost:8080`).
- **Client registration**: the post-logout URIs become `/bff/backoffice/signout-callback-oidc` and `/bff/storefront/signout-callback-oidc` on the gateway origin.
