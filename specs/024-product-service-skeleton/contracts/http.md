# Contract: Product service HTTP

The browser reaches the service only through the gateway at `/api/product/**`, which removes the prefix and adds the session's bearer token. The API publishes no host port.

## `GET /health/live`, `GET /health/ready`

Allow-listed.
- `live` is 200 while the process runs.
- `ready` is 200 only after the migrations ran and the database answers; otherwise 503.

## `GET /roles` (gateway: `/api/product/roles`)

Tenant route; permission `product.roles.view`.

**200**:

```json
{
  "roles": [
    { "name": "product.administrator", "displayName": "Administrator", "permissions": [ { "name": "product.roles.view", "displayName": "View roles" } ] },
    { "name": "product.viewer", "displayName": "Viewer", "permissions": [ { "name": "product.roles.view", "displayName": "View roles" } ] }
  ],
  "canAssign": false
}
```

**Errors**:

| Status | When |
|---|---|
| 401 | no token; or a token that is expired, wrongly signed, for another audience, or malformed as an identity (`invalid-identity`) |
| 403 | a platform user (tenant route); or a tenant user without `product.roles.view`; the problem details list the missing permission |

Until 025, every tenant user gets 403, because nobody holds a Product role.
