# Protocol and pages: Identity Customers

## `GET /connect/authorize`

New parameter `tenant=<slug>`.

| Client | `tenant` | Result |
|---|---|---|
| `storefront-bff` | missing or unknown | redirect to the client with `error=invalid_request` |
| `storefront-bff` | known | normal flow; the login challenge return URL keeps `tenant` |
| `backoffice-bff` | any | ignored |

A login session whose user type the client doesn't accept, or (storefront) whose tenant differs, is treated as no session: redirect to `/account/login`.

## `POST /connect/token`

Code and refresh grants re-check the client policy; a mismatch is `invalid_grant`.

## `/account/login`

- Storefront target (return URL is a storefront authorization request with a known tenant): the heading shows the tenant name, the account is looked up as a customer of that tenant, and a "Create an account" link goes to `/account/register` with the same return URL.
- Otherwise: staff and platform lookup, as in 012.
- Every failure shows the generic error.

## `/account/register`

- GET `?returnUrl=...` or `?tenant=<slug>`: the form (email, password, confirm password) with the tenant name, or "This shop does not exist." without a form.
- POST: antiforgery; field errors next to fields; "An account with this email already exists. Sign in instead." with a link to the login page; success signs in and redirects to the local return URL or `/account/signed-in`.
- Publishes `UserRegistered { userId, tenantId, userType: "customer", email }`.
