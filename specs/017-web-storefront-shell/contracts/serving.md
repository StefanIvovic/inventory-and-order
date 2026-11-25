# Contract: Serving, development and end-to-end additions

## Storefront container (`storefront-spa`, port 8080, profile `apps`)

| Request | Answer |
|---|---|
| `/shop/` and any non-file path under it | `index.html`, `Cache-Control: no-cache` |
| `/shop/<name>-<HASH8>.js` or `.css` | `Cache-Control: public, max-age=31536000, immutable` |
| `/health` | 200 |
| anything else | 404 |

- **Headers**: the same security headers as the backoffice (016 contracts/serving.md).
- **Container**: non-root, no published port, listens on IPv4 and IPv6.
- **Gateway**: reaches it at `http://storefront-spa:8080`.

## Development mode

`-WebDev` also starts `storefront-dev` (`ng serve` on 4200 with serve path `/shop/`), and the gateway's `storefront-spa` upstream points to it.

## End-to-end

No change to the stack or the runner. A new spec, `tests/e2e/specs/storefront-register.spec.ts`, uses `DEMO_TENANT_SLUG` from `dev.env`.
