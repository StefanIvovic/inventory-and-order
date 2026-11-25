# Feature Specification: Identity Customers

**Feature Branch**: `014-identity-customers`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "014 identity-customers. Customers of a tenant register and sign in through the storefront. The storefront client passes the tenant as a custom authorization parameter; customer self-registration on a server-rendered page; customer emails unique per tenant by database constraint; customer user name `<tenantId>:<email>`; user type per client (backoffice: staff and platform; storefront: customers of the requested tenant) with the generic login error for mismatches; customers have no Identity permissions. Out of scope: per-tenant registration switch, customer profile routes, email confirmation and password reset, registration rate limiting and captcha, the gateway."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A shopper registers on a tenant's storefront (Priority: P1)

A shopper on acme's storefront chooses to sign in, has no account yet, and follows "Create an account". They enter an email, a password and the password again. Identity creates their customer account in acme, signs them in, and sends them back to the storefront signed in. Other services learn that a customer registered.

**Why this priority**: Customers can't buy anything without an account, and there is no other way to create one.

**Independent Test**: Acting as the storefront client, start an authorization request for tenant `acme`, follow the link from the login page to registration, register `shopper@mail.test`, and redeem the code. The token carries acme's tenant and `user_type=customer`. One `UserRegistered` with user type `customer` is published.

**Acceptance Scenarios**:

1. **Given** a storefront authorization request for an existing tenant, **When** the shopper opens the registration page from the login page, **Then** the page shows the tenant's name and a form for email, password and password confirmation.
2. **Given** a valid email not yet used by a customer of that tenant and a password that meets the policy, **When** the shopper registers, **Then** a customer account is created in that tenant, the shopper is signed in, the authorization continues, and `UserRegistered` (customer) is published.
3. **Given** the passwords don't match, the email is invalid, or the password breaks the policy, **When** the shopper submits, **Then** the page shows every error next to its field and nothing is created.
4. **Given** the email is already registered as a customer of that tenant, **When** the shopper registers again, **Then** the page says an account with this email already exists and offers sign-in, and nothing is created.
5. **Given** the email is used by a staff account, a platform account, or a customer of another tenant, **When** the shopper registers in this tenant, **Then** registration succeeds and creates a separate customer account.
6. **Given** an unknown tenant slug, **When** the registration page is opened, **Then** it says the shop doesn't exist and shows no form.

---

### User Story 2 - A customer signs in to their tenant's storefront (Priority: P1)

A registered customer of acme signs in through acme's storefront with their email and password and gets a session for acme.

**Why this priority**: Registration is useless without sign-in, and every later storefront feature needs a signed-in customer.

**Independent Test**: Register a customer in `acme` and, with the same email, separate customers in `globex` and a staff account. Sign in through the storefront for `acme`: the token is for acme's customer account, not any other.

**Acceptance Scenarios**:

1. **Given** a customer of acme, **When** they sign in through the storefront with tenant `acme`, **Then** they get tokens with acme's tenant and `user_type=customer`.
2. **Given** the same email exists as customers of acme and globex with different passwords, **When** the customer signs in on acme's storefront with acme's password, **Then** they are signed in to the acme account; with globex's password they get the generic error.
3. **Given** a customer of globex, **When** they sign in on acme's storefront, **Then** they get the generic error, the same as for an unknown email.
4. **Given** a wrong password five times, **When** the customer tries again, **Then** the account is locked like any other account (012 policy).

---

### User Story 3 - Each client accepts only its kind of user (Priority: P1)

The backoffice is for staff and the platform admin; the storefront is for customers of one tenant. Identity refuses to hand a session for one kind of user to the other client, without revealing that the account exists.

**Why this priority**: Without it, a customer could get a backoffice session or a customer of one shop a session in another. That is a security boundary, not a convenience.

**Independent Test**: A staff user tries the storefront and a customer tries the backoffice: both see the generic error and no code is issued. A shopper already signed in to acme starts a storefront request for globex: they are asked to sign in again and no code for acme's account is issued to globex's request.

**Acceptance Scenarios**:

1. **Given** a storefront authorization request without a tenant, or with an unknown tenant, **When** it reaches Identity, **Then** it is rejected and no sign-in page is shown.
2. **Given** a staff or platform user, **When** they sign in on the storefront, **Then** they see the generic error.
3. **Given** a customer, **When** they sign in on the backoffice, **Then** they see the generic error.
4. **Given** a signed-in session that does not match the request (a customer of another tenant, or the wrong user type for the client), **When** an authorization request arrives, **Then** Identity asks for sign-in again and never issues a code for the mismatching account.
5. **Given** a backoffice authorization request with a tenant parameter, **When** it reaches Identity, **Then** the parameter is ignored and staff sign-in works as before.

---

### User Story 4 - Customers stay out of staff management (Priority: P2)

Customers are users of the tenant but not staff. Staff management in Identity lists and changes staff only, and customers have no Identity permissions.

**Why this priority**: Mixing customers into staff lists would let an administrator give a customer staff roles. It is a safety rule more than a feature.

**Independent Test**: With staff and customers in acme, the administrator's user list contains only staff. Disabling or assigning a role to a customer's id returns 404. A customer token gets 403 on every Identity route.

**Acceptance Scenarios**:

1. **Given** staff and customers in a tenant, **When** an administrator lists users, **Then** only staff appear.
2. **Given** a customer's id, **When** an administrator disables it or assigns it a role, **Then** the user is not found.
3. **Given** a customer token, **When** it calls any Identity API route, **Then** the answer is 403.

---

### Edge Cases

- **Two registrations with the same email in the same tenant at once.** One account is created; the other submission sees "already exists". The database constraint decides.
- **Email case and spacing.** `Shopper@Mail.test ` and `shopper@mail.test` are the same customer email within a tenant.
- **The registration page opened directly**, without an authorization request but with a tenant. Registration works and the shopper lands on the "signed in" page.
- **The registration page opened without a tenant.** It says a shop must be chosen and shows no form.
- **A return URL pointing outside Identity.** Ignored, as on the login page.
- **The login page opened directly without a tenant.** It signs in staff and platform users only, as in 012 and 013.
- **A customer whose email later becomes a staff email.** Unaffected: the accounts are separate.
- **Account enumeration.** Sign-in never reveals whether an email exists in another tenant or as another user type. Registration reveals only that the email is already a customer of the same tenant, which the shopper needs to know to sign in instead.

## Requirements *(mandatory)*

### Functional Requirements

**Tenant in the authorization request**

- **FR-001**: The storefront client MUST send the tenant slug as the authorization parameter `tenant`. A storefront request without it, or with an unknown slug, MUST be rejected with an error to the client and no sign-in page.
- **FR-002**: Identity MUST carry the tenant of the request to the login page and the registration page, and show the tenant's name on both.
- **FR-003**: The backoffice client MUST ignore the `tenant` parameter.

**Registration**

- **FR-004**: Identity MUST serve a server-rendered registration page with email, password and password confirmation, protected against cross-site request forgery, in the same style as the login page, linked from the storefront login page and back.
- **FR-005**: Registration MUST create a customer account in the request's tenant, sign the customer in, and continue the authorization request, or land on the "signed in" page when there is none. Only local return URLs are followed.
- **FR-006**: Registration MUST validate the email format, the password policy from 012, and that both passwords match, and show every error next to its field.
- **FR-007**: Registration MUST publish `UserRegistered` with user type `customer` through the outbox, in the same transaction as the account.
- **FR-008**: An email already registered as a customer of the same tenant MUST be refused with a message that the account exists and a link to sign in, also when two registrations race.

**Uniqueness and account identity**

- **FR-009**: Customer emails MUST be unique within a tenant, enforced by a database constraint on tenant plus normalized email for customers. Staff and platform emails stay globally unique (013). The same email MAY exist once as staff or platform and once per tenant as a customer.
- **FR-010**: Each account MUST keep a globally unique internal user name: the email for staff and platform users, and the tenant id plus the email for customers.

**Sign-in and user type per client**

- **FR-011**: Storefront sign-in MUST look the account up among the customers of the request's tenant by email. Backoffice and direct sign-in MUST look it up among staff and platform users.
- **FR-012**: The backoffice client MUST issue codes only for staff and platform users; the storefront client only for customers of the requested tenant. Any mismatch MUST show the generic sign-in error at the login page and MUST NOT issue a code.
- **FR-013**: An existing sign-in session that does not match the request (user type or tenant) MUST be treated as no session: Identity asks for sign-in again.
- **FR-014**: The 012 lockout policy applies to customers in the same way. The 013 disabled check applies to every account at sign-in, authorization and refresh; disabling customers is out of scope in 014.

**Separation from staff management**

- **FR-015**: Staff management routes (list, disable, role assignment) MUST act on staff only. A customer's id MUST return 404.
- **FR-016**: Customers MUST have no Identity permissions. Every Identity API route returns 403 for a customer token, and the no-roles route test stays green.

### Key Entities

- **Customer account**: a user with type `customer`, a tenant, an email unique within that tenant, a password, a security stamp and lockout state. Internal user name: tenant id plus email.
- **Tenant** (from 013): looked up by slug from the authorization request.
- **Event**: `UserRegistered` with user type `customer` (contract from 013, unchanged).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A shopper registers and comes back signed in within one automated test of the full storefront flow.
- **SC-002**: 100% of duplicate customer emails within a tenant, including concurrent ones, are refused with exactly one account created.
- **SC-003**: The same email works as independent accounts in at least two tenants and as a staff account at the same time.
- **SC-004**: In 100% of tested mismatches (wrong user type, wrong tenant, mismatching existing session), no code is issued and the response is indistinguishable from a wrong password.
- **SC-005**: Every Identity API route answers 403 to a customer token, and staff lists contain no customers.

## Assumptions

- Every tenant accepts customer self-registration. A per-tenant switch comes later, when a tenant needs it.
- No email confirmation or password reset yet; there is no email sending.
- Registration reveals that an email is already a customer of the same tenant. Sign-in reveals nothing. Rate limiting for both comes in phase 5.
- Customer permissions in other services (the fixed default set per service) are defined by those services, not by Identity.
- The gateway that adds the `tenant` parameter is feature 015; until then, the tests act as the storefront client.
- The `UserRegistered` contract from 013 is unchanged, so no new contracts version is needed.
