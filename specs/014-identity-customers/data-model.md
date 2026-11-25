# Data Model: Identity Customers

## AppUser (changed)

| Field | Rules |
|---|---|
| `AppUser.UserName` | Staff and platform: the email. Customer: `<TenantId>:<email>`. Globally unique (existing index `IX_identity_users_NormalizedUserName`). `UserName` and `NormalizedUserName` widen to 320 characters so a 256-character email plus the 33-character prefix fits |
| `AppUser.Email` | Required, valid email, max 256 |
| `AppUser.NormalizedEmail` | Staff and platform: unique among non-customers (`ux_identity_users_staff_email`, 013). Customers: unique per tenant (`ux_identity_users_customer_email` on `TenantId`, `NormalizedEmail` `WHERE "UserType" = 'customer'`) |
| `AppUser.UserType` | `customer` for accounts created by registration |
| `AppUser.TenantId` | Required for customers (existing check constraint) |

## RegisterCustomer (command)

| Field | Rules |
|---|---|
| `RegisterCustomer.TenantId` | Resolved by the page from the slug; must exist |
| `RegisterCustomer.Email` | `AppUser.Email` rules |
| `RegisterCustomer.Password` | The 012 policy |
| `RegisterCustomer.ConfirmPassword` | Equal to `Password` |

## Client policies (code)

| Client | Accepted user types | Tenant parameter |
|---|---|---|
| `backoffice-bff` | staff, platform | ignored |
| `storefront-bff` | customer | required, must match the account's tenant |
