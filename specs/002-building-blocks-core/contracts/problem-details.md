# Contract: Error responses

Content type: `application/problem+json`

| Source | Status | `code` | `detail` | Log level |
|---|---|---|---|---|
| `DomainException` with Kind Conflict | 409 | exception `Code` | exception message | Information |
| `DomainException` with Kind Rule | 422 | exception `Code` | exception message | Information |
| `NotFoundException` | 404 | `not-found` | `The requested {resource} was not found.` | Information |
| Malformed identity | 401 | `invalid-identity` | `The caller's identity is invalid.` | Warning |
| Any other exception | 500 | absent | absent | Error, with the exception |

Every body contains `type`, `title`, `status` and `traceId`.

Example (422):

```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Not enough stock in the source warehouse.",
  "code": "inventory.transfer.insufficient-stock",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```
