# Contract: Serving, development and end-to-end stacks

## Backoffice container (`backoffice-spa`, port 8080, profile `apps`)

| Request | Answer |
|---|---|
| `/backoffice/` and any path under it that is not a file | `index.html`, `Cache-Control: no-cache` |
| `/backoffice/<name>-<HASH8>.js` or `.css` | the file, `Cache-Control: public, max-age=31536000, immutable` |
| other files under `/backoffice/` | the file with default caching |
| `/health` | 200 |
| anything else | 404 |

Every answer carries:
- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: same-origin`
- `Content-Security-Policy: default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'`

The container runs as a non-root user and publishes no port. The gateway reaches it at `http://backoffice-spa:8080`.

## Development mode (`./scripts/dev.ps1 up -Profile infra,apps -WebDev`)

- `backoffice-dev` runs `ng serve` on port 4200 with serve path `/backoffice/`, polling, and the source bind-mounted.
- The gateway's `backoffice-spa` upstream is overridden to `http://backoffice-dev:4200`.
- The browser still uses `http://localhost:8080/backoffice/`.

## End-to-end (`./scripts/e2e.ps1`)

- Compose project `e2e-test`, files `deploy/compose.yaml` and `tests/e2e/compose.e2e.yaml`, profiles `infra,apps,e2e`.
- No published ports. Data and key paths are on `tmpfs`; there are no named volumes.
- `playwright` shares the gateway's network namespace, with `BASE_URL=http://localhost:8080`.
- The runner always ends with `down -v --remove-orphans` and the cleanup check for `e2e-test`, and exits with the test's exit code.
