# Data Model: Identity Authentication

## AppUser (table `identity_users`)
| Field | Type | Rules |
|---|---|---|
| `Id` | uuid | primary key, becomes `sub` |
| `Email` / `NormalizedEmail` | text(256) | required, `NormalizedEmail` globally unique |
| `UserName` / `NormalizedUserName` | text(256) | equals the email, unique |
| `PasswordHash` | text | required |
| `UserType` | text(20) | one of `staff`, `customer`, `platform` (check constraint) |
| `TenantId` | text(64) null | required unless `UserType = platform`, must be null for platform (check constraint) |
| `SecurityStamp` | text | changes on password or disable changes, checked at refresh |
| `ConcurrencyStamp` | text | optimistic concurrency |
| `AccessFailedCount` | int | reset on success |
| `LockoutEnabled` / `LockoutEnd` | bool / timestamptz null | 5 failures → `LockoutEnd = now + 10 min` |
| `EmailConfirmed`, `PhoneNumber`, `TwoFactorEnabled` … | Identity defaults | unused in 012 |

- Not tenant-filtered (sign-in looks users up across tenants).
- Related Identity tables: `identity_user_claims`, `identity_user_logins` and `identity_user_tokens`, required by the user store and unused in 012.

## OpenIddict entities (tables `openiddict_applications`, `openiddict_authorizations`, `openiddict_scopes`, `openiddict_tokens`)
Owned by OpenIddict, with `Guid` keys.

## Scope registrations
| Scope | Resource (audience) |
|---|---|
| `identity` | `identity` |
| `product` | `product` |
| `inventory` | `inventory` |
| `order` | `order` |
| `payment` | `payment` |

`openid` and `offline_access` are standard and have no resource.

## Client registrations (from configuration `Identity:Clients:<id>`)
| Client | Type | Consent | Permissions | Requirements |
|---|---|---|---|---|
| `backoffice-bff` | confidential | implicit | authorize, token, end-session endpoints; code and refresh grants; response type `code`; scopes `openid`, `offline_access` and all API scopes | PKCE |
| `storefront-bff` | same | same | same | same |

Per client: `Secret` (stored hashed by OpenIddict), `RedirectUri` and `PostLogoutRedirectUri`. All three are required in config.

## DataProtectionKey (table `data_protection_keys`)
The standard `IDataProtectionKeyContext` entity.

## Access token (JWT, `typ: at+jwt`, signed, not encrypted)
| Claim | Source |
|---|---|
| `iss` | `Identity:Issuer` |
| `sub` | `AppUser.Id` |
| `user_type` | `AppUser.UserType` |
| `tenant_id` | `AppUser.TenantId` (omitted for platform) |
| `aud` | resources of the granted API scopes |
| `scope`, `client_id`, `jti`, `iat`, `nbf`, `exp` | protocol |
| *(never)* | roles, permissions, email, name |

Lifetime: 15 minutes. Refresh token: 12 hours rolling, rotated on use, zero reuse leeway.

## Configuration
| Key | Dev source | Production |
|---|---|---|
| `Identity:Issuer` | `IDENTITY_ISSUER` | required |
| `Identity:PlatformAdmin:Email` / `:Password` | `PLATFORM_ADMIN_EMAIL` / `PLATFORM_ADMIN_PASSWORD` | required on the first start only |
| `Identity:Clients:backoffice-bff:*`, `Identity:Clients:storefront-bff:*` | `BACKOFFICE_BFF_SECRET`, `STOREFRONT_BFF_SECRET`, `GATEWAY_ORIGIN` | required |
| `Identity:Keys:Mode` | `Directory` | `Certificates` |
| `Identity:Keys:Path` / `:Password` | `/keys`, `IDENTITY_KEYS_PASSWORD` | the paths of the mounted certificates, plus the password |
