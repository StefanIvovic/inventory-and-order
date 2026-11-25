# Research: Identity Customers

## R1. Client policies and the tenant parameter

- **Decision**: `ClientPolicies` maps `backoffice-bff` to {staff, platform} with no tenant, and `storefront-bff` to {customer} with a required tenant. In `AuthorizeAsync`:
  1. Read `request.GetParameter("tenant")`. For a storefront request without it, or with a slug that matches no tenant, return `invalid_request` to the client before any login challenge. Backoffice requests ignore it.
  2. After the login session is found: if its user type isn't accepted by the client, or (storefront) its tenant differs from the requested tenant, challenge again as if there were no session (FR-013).
  3. The token endpoint re-checks the same policy at code redemption and refresh, so a code issued by mistake could never be redeemed by the other client.
- **Rationale**: OpenIddict accepts custom parameters and keeps them in the request; the challenge return URL already contains the whole query, so the login and register pages receive the tenant without extra state.
- **Alternatives considered**: `acr_values=tenant:<slug>` (standard but clumsier, rejected by the owner); a tenant claim in the client registration (one storefront client serves all tenants).

## R2. User names and sign-in lookup

- **Decision**: `AppUser.UserName` is the email for staff and platform users and `<tenantId>:<email>` for customers (`AppUser.ForTenant` builds it). The login page:
  1. Parses its local return URL with `SignInTarget.From(returnUrl)`: when it is an authorization request from `storefront-bff` with a known tenant, the target is "customers of tenant X", otherwise "staff and platform".
  2. Calls `PasswordSignInAsync(userName, ...)` with `<tenantId>:<email>` or the plain email. A staff email never matches a customer name and the reverse, so the wrong account is never found and the error is the generic one (FR-011, FR-012).
- `IdentityOptions.User.RequireUniqueEmail` becomes `false`: the default validator calls `FindByEmailAsync`, which throws once two accounts share an email. Uniqueness is guaranteed instead by:
  - the user-name unique index (staff email globally; customer email per tenant through the prefix), reported by `UserManager` as `DuplicateUserName` → `email-taken`, and
  - the two partial email indexes in the database (R4).
- `AllowedUserNameCharacters` must allow `:`; the default set includes neither `:` nor every email character, so it is set to an empty string (no character check), and the email validator keeps the format check.
- Tests that call `FindByEmailAsync` switch to `FindByNameAsync`.
- **Alternatives considered**: a custom user store with tenant-aware lookups (more code, same result).

## R3. Registration

- **Decision**:
  - `Pages/Account/Register`: GET shows the form when the tenant (from the `tenant` query value or the return URL's authorization request) exists, otherwise a "shop not found" message. POST validates the antiforgery token, sends `RegisterCustomer(TenantId, Email, Password, ConfirmPassword)` through `IMessageBus.InvokeAsync`, catches `ValidationException` (field errors into `ModelState`) and the `email-taken` conflict (from the handler check or the unique index), then signs in with `SignInManager.SignInAsync` and redirects to the local return URL or the "signed in" page.
  - `RegisterCustomerHandler` (plain Wolverine handler, not `CommandHandler<T>`): validates through `ICommandValidation<RegisterCustomer>`, checks the tenant exists, creates the customer with `HandlerUserManager` (no autosave), and returns `UserRegistered` with the envelope tenant set. Wolverine's transactional middleware saves the user and the outbox message together.
  - Validator: email format and length, password present, confirmation equal to password; the 012 policy errors come from `UserManager` through `IdentityResultErrors`.
  - The page is in `/Account`, already allow-listed (012 research R14).
- **Rationale**: keeps creation in a handler with the outbox (principle III and IV); the page keeps only input and sign-in.

## R4. Customer email index

- **Decision**: `ux_identity_users_customer_email` unique on (`TenantId`, `NormalizedEmail`) `WHERE "UserType" = 'customer'`, in migration `Customers`. `UniqueViolationMapper` maps it to 409 `email-taken`; the register page turns that into the "already exists" message.

## R5. Staff management stays staff-only

- **Decision**: `TenantUsers.FindAsync` and `ListUsersHandler` filter `UserType == staff`. Customers therefore return 404 on disable and role routes. `IdentityPermissionProvider` returns no permissions for customers (no assignment can exist, but the rule is explicit).

## R6. Tests

- `CodeFlow` gains the storefront client and the `tenant` parameter in `AuthorizeRequest`.
- Full storefront flow: authorize (storefront, tenant) → login page → link to register → register → back to authorize → code → token claims.
- Policies: staff on storefront, customer on backoffice, customer of another tenant, mismatching session, missing and unknown tenant, tenant ignored on backoffice.
- Concurrency: parallel registrations with one email in one tenant.
