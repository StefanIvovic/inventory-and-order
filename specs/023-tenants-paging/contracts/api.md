# Contract: `GET /tenants` (Identity, through the gateway as `/api/identity/tenants`)

Platform-only (`[PlatformOnly]`), permission `identity.tenants.view`.

## Request

| Parameter | Default | Rule |
|---|---|---|
| `q` | none | identifier or name prefix, ignoring case; trimmed; at most 200 characters |
| `size` | 25 | 1–100 |
| `after` | none | `nextCursor` from the previous answer |
| `includeTotal` | `false` | adds `total` |

## Answer 200

```json
{
  "items": [
    { "id": "6f1c…", "slug": "acme", "name": "Acme Ltd", "createdAt": "2026-11-27T09:00:00+00:00" }
  ],
  "nextCursor": "eyJzbHVnIjoiYWNtZSJ9",
  "total": 60,
  "actions": { "canCreate": true }
}
```

- `items` are in identifier order (byte order).
- `nextCursor` is `null` on the last page.
- `total` is present only with `includeTotal=true`.

## Errors

| Status | When | Body |
|---|---|---|
| 401 | no token | as today |
| 403 | not a platform user, or no `identity.tenants.view` | problem details listing the missing permission |
| 400 | `size` outside 1–100, or `q` longer than 200 | `validation-failed` with field errors |
| 422 | `after` is not a valid token | `invalid-cursor` |

## Change from today

- **Before**: every tenant, newest first, with no `nextCursor` or `total`.
- **Now**: one page in identifier order. The only client is the backoffice, which changes in the same feature.
