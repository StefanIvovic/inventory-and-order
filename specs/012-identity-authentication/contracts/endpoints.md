# Contracts: Identity Authentication

## Protocol endpoints (all allow-listed)

| Method | Path | Purpose | Notes |
|---|---|---|---|
| GET | `/.well-known/openid-configuration` | discovery | issuer = `Identity:Issuer` |
| GET | `/.well-known/jwks` | public signing keys | signing key only |
| GET/POST | `/connect/authorize` | code flow start | `response_type=code`, `code_challenge_method=S256` required, registered `redirect_uri` only, at least one API scope |
| POST | `/connect/token` | code redemption and refresh | client secret required; refresh rotates and is refused on reuse, stamp change or lockout |
| GET | `/connect/endsession` | shows the sign-out confirmation | registered `post_logout_redirect_uri` only |
| POST | `/connect/endsession` | signs out and redirects | antiforgery-protected |

## Pages (allow-listed)

| Path | Purpose |
|---|---|
| `GET/POST /account/login?ReturnUrl=` | email and password form. One generic error. Lockout message. Local return URLs only |
| `GET /account/signed-in` | landing page when there is no return URL |
| `/css/identity.css` | static stylesheet |

## Token response (`/connect/token`)
```json
{ "access_token": "<jwt>", "token_type": "Bearer", "expires_in": 900, "refresh_token": "<opaque>", "id_token": "<jwt>", "scope": "openid offline_access identity order" }
```

## BuildingBlocks 0.2.0 (additive)

```csharp
IServiceCollection AddBuildingBlocksRouteAuthorization(this IServiceCollection services, string authenticationScheme);
```

`AddBuildingBlocksAuthentication(configuration)` now calls `AddBuildingBlocksRouteAuthorization(JwtBearerDefaults.AuthenticationScheme)`. Its behaviour is unchanged. `TestTokens` tokens carry the header `typ: at+jwt`.
