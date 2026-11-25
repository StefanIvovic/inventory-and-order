# Contract: Lookup API (each service; Identity first)

Through the gateway: `/api/identity/lookups...`. Category `SignedIn`: any valid signed-in user; anonymous gets 401.

## GET /lookups/{typeCode}/values

Query: `q`, `ids` (repeated), `codes` (repeated), `size` (1–200, default 50), `after`.

200
```json
{
  "items": [
    { "id": "…", "code": "active", "name": "Active" },
    { "id": "…", "code": "disabled", "name": "Disabled" }
  ],
  "nextCursor": null
}
```

| Case | Status | Body |
|---|---|---|
| unknown `typeCode` | 404 | problem, `code: lookup-type-not-found` |
| `size` out of range, more than 200 `ids` or `codes`, `q` over 100 characters, malformed `after` | 400 | problem, `code: validation-failed`, `errors.<field>` |
| no sign-in | 401 | |
| filters match nothing | 200 | `{ "items": [], "nextCursor": null }` |

## Caching

- 200 carries `ETag: "<hash>"`, `Cache-Control: private, max-age=300` and `Vary: Cookie, Authorization`.
- A request with `If-None-Match` matching the current ETag gets 304, with no body.

## Usage by screens

- Lists are addressed by type code. Ids are only for referring to individual values. No route lists the types.
- **Search box**: `?q=<text>&size=20`.
- **Select showing chosen values**: `?ids=<id>&ids=<id>`, or `?codes=…`.
- **Long lists**: follow `nextCursor` with `after`.
