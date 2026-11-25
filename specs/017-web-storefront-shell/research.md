# Research: Storefront Shell

This feature reuses 016's research. The versions were checked again on 2026-11-25 and are unchanged:
- Angular, CLI and Material 22.3.0, with TypeScript 6.0, Vitest 5 and jsdom
- angular-eslint 22.8.0
- `@playwright/test` 1.65.1
- `node:26.13.1-alpine3.24`, `nginxinc/nginx-unprivileged:1.30.6-alpine3.24`, `mcr.microsoft.com/playwright:v1.65.1-noble`

Only what differs from 016 is recorded here. Each storefront item names the 016 lesson it applies.

## R1. Project generation

- **Generate it like the backoffice**: `ng new storefront --directory src/web/storefront` in `NODE_IMAGE` with the same flags, then `ng add @angular/material` and `ng add angular-eslint@22.8.0`. Apply the 016 R11 clean-up:
  - remove `.vscode`, `README.md` and the project `.gitignore`
  - strip every generated comment
  - drop Material's Google Fonts links and use `system-ui` in the theme
  - set `optimization.styles.inlineCritical: false` in production, so no inline `onload` conflicts with the CSP
- **Base href**: `index.html` has `<base href="/shop/">`. A shop's pages are `/shop/<slug>/...`, and the built files live directly under `/shop/` (`/shop/main-<HASH>.js`).
  - A file name can never be a shop identifier, because shop identifiers contain no `.`.
- **Rejected**: copying the backoffice folder wholesale. That would carry over its specs and names, and generating the project shows any CLI drift.

## R2. Shop identifier and routes

- **Shop matcher**: `shop.ts` exports `SHOP_SLUG_PATTERN`, the same rule as Identity and the gateway, `^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$`. It also exports a `UrlMatcher` that accepts the first segment only when it matches.
- **Routes**:
  - `{ matcher: shopMatcher, children: [{ path: '', component: Home }, { path: 'account', component: Account }, { path: '**', component: Home }] }`
  - `{ path: '**', component: ShopNotFound }`

  So `/shop/` and `/shop/Demo/` fall through to "shop not found".
- **The slug for the session** is read once from the browser location at start: the first segment after `/shop/`. The router only renders pages for that shop. Changing shop is a full page load, either through links outside the app or by typing an address.

## R3. Session module (copied from the backoffice and adapted)

- **Changes from the backoffice copy**:
  - `SessionService` takes the slug from `BROWSER_LOCATION` at construction.
  - The user endpoint is `/bff/shop/<slug>/user`, and login is `/bff/shop/<slug>/login`.
  - The interceptor sets `X-Bff-App: storefront`.
- **States** (see data-model.md):
  - `loading`
  - `no-shop` (bad slug, no request made)
  - `visitor` (401 from the user endpoint, or a session for another shop, which the gateway also answers with 401, 015 FR-010)
  - `signed-in`
  - `unavailable` (any other answer)
- **Visitor rendering**: unlike the backoffice, `visitor` and `unavailable` both render the full shop frame. `unavailable` adds the banner "The shop is having trouble right now" with "Try again".
- **API answers**:
  - A 401 from `/api/**` switches to `visitor` on the same page.
  - A 503 `identity-unavailable` uses the same wait-for-retry logic as the backoffice.
- **Sign-in error wording** (contracts/ui.md):
  - `access_denied`: "This account can't be used in this shop."
  - `invalid_request`: "This shop isn't available for sign-in."
  - `server_error`: "Sign-in is temporarily unavailable. Please try again."
  - any other code: "Sign-in failed. Please try again."

## R4. Serving

- **nginx**: `nginx.conf` and `security-headers.conf` are copied from the backoffice, with `/backoffice/` replaced by `/shop/`.
  - `location = /shop/index.html` has `no-cache`.
  - `location ~ "^/shop/.+-[A-Z0-9]{8}\.(js|css)$"` is `immutable`.
  - `location /shop/` uses `try_files $uri $uri/ /shop/index.html`.
  - The server listens on `8080` and `[::]:8080` (016 R11) and keeps `/health`.
- **Gateway**: the `storefront-spa` cluster becomes `http://storefront-spa:8080` in `appsettings.json` and `src/gateway/compose.yaml`.

## R5. Development mode

`deploy/compose.web-dev.yaml` gains `storefront-dev`:
- the same Node container pattern as `backoffice-dev`
- serve path `/shop/`
- `--allowed-hosts storefront-dev`
- polling every 1000 ms

It also sets the gateway's `ReverseProxy__Clusters__storefront-spa__Destinations__main__Address` to `http://storefront-dev:4200`. `-WebDev` starts both dev servers.

## R6. CI

- **`.github/workflows/storefront.yml`**: the same steps as `backoffice.yml`, run from `src/web/storefront`.
  - Its smoke checks read every answer into a variable and match `<base href="/shop/"` without the closing characters (016 R11).
  - It keeps `set -x`.
- **`smoke-signin.ps1`** gains a check that `/shop/demo/` serves the storefront host page.
- **`e2e.yml`** already watches `src/web/**`.

## R7. End-to-end

`tests/e2e/specs/storefront-register.spec.ts`:
1. Open `/shop/<DEMO_TENANT_SLUG>/` as a visitor and see "Sign in".
2. Press "Sign in". On Identity's page, follow "Create an account".
3. Fill "Email" with a random `@mail.test` address, then "Password" and "Confirm password", and press "Create account".
4. Land back on `/shop/<slug>/` with the account menu showing the email.
5. Open "Account": it shows the email and the slug.
   - Open `/shop/other-shop/`: it shows "Sign in", because a session for one shop is not one for another (SC-006). Go back to `/shop/<slug>/`.
6. Sign out: the menu, then Identity's "Sign out" confirmation.
7. End on `/shop/<slug>/` as a visitor.

The demo tenant slug comes from `dev.env` through the Playwright service's `env_file` (016 R7).

## R8. Implementation findings

- **Generation matches 016.** `ng new` and the two `ng add` commands produced the same `package.json` as the backoffice, apart from the name. The 016 clean-up applied unchanged, and no file has a comment.
- **The router drops the trailing slash.** With base `/shop/` and the shop route matched by `shopMatcher`, Angular changes `/shop/demo/` to `/shop/demo` in the address bar.
  - nginx serves both forms.
  - The gateway's return-path rule needs `/shop/<slug>/` as the prefix, so a return path of `/shop/demo` falls back to the shop's start page, which is the same page.
  - The end-to-end test matches `/shop/<slug>/?$`.
- **Sign-in errors are shown only to visitors.** A stale `?signin-error=` while signed in is removed from the address without a message. That is intended, since the user is already signed in.
- **A well-formed unknown shop** (`/shop/nope-shop/`) shows the frame. Its "Sign in" comes back with "This shop isn't available for sign-in." (014 rejects the tenant at authorize, and the gateway maps it to `invalid_request`), as the spec expected.
- **Live reload** for the storefront took about 1.5 seconds through the gateway, with both dev servers running together.
- **End-to-end**: both specs passed in about 12 seconds on a throwaway stack while the dev stack ran. That includes registration through Identity's "Create an account", and seeing "Sign in" on another shop with the demo session (SC-006).
- **`UrlMatcher` typing.** Declaring the matcher as `UrlMatcher` keeps the router's three-argument signature, which the tests call directly.

