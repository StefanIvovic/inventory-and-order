# Feature Specification: Identity Tenants and Staff

**Feature Branch**: `013-identity-tenants-staff`

**Created**: 2026-11-24

**Status**: Draft

**Input**: User description: "013 identity-tenants-staff. Identity manages tenants and staff users: the platform admin creates and lists tenants, each with its first staff admin; tenant administrators create, list and disable staff and assign Identity's own fixed roles (Administrator, Viewer); disabled users can't sign in or refresh; TenantCreated, UserRegistered and UserDisabled are published through the outbox from a new Identity.Contracts package; staff and platform emails are unique by database constraint; request validation runs after the permission check; a demo tenant is seeded in dev. Out of scope: customers and storefront registration, user type per client (014), the gateway (015), tenant disabling, password reset and email confirmation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The platform admin creates a tenant with its first admin (Priority: P1)

The platform admin creates a tenant by giving its slug and name, plus the email and password of the tenant's first staff admin. Identity creates the tenant and the admin, gives the admin the Administrator role, and tells the other services that a tenant and a user exist. The platform admin can list all tenants.

**Why this priority**: Without a tenant and a first admin, no tenant user can sign in, and no other service can grant anyone a role.

**Independent Test**: As the platform admin, create tenant `acme` with admin `admin@acme.test`. Then sign in through the code flow as that admin: the token carries `tenant_id` of the new tenant and `user_type=staff`, and `GET /users` returns 200. The test also observes one `TenantCreated` and one `UserRegistered` message.

**Acceptance Scenarios**:

1. **Given** the platform admin, **When** they create a tenant with a free slug, a name and a valid admin email and password, **Then** the tenant and its admin exist, the admin holds the Administrator role in that tenant, and `TenantCreated` and `UserRegistered` are published.
2. **Given** a tenant with slug `acme` already exists, **When** the platform admin creates another tenant with slug `acme`, **Then** the request is refused as a conflict and nothing is created or published.
3. **Given** the admin email is already used by any staff or platform user, **When** the platform admin creates a tenant with it, **Then** the request is refused as a conflict and nothing is created or published.
4. **Given** an invalid slug, name, email, or a password that breaks the policy, **When** the platform admin creates a tenant, **Then** the request is refused with every field error listed, and nothing is created.
5. **Given** the platform admin, **When** they list tenants, **Then** they see every tenant's slug, name and creation time.
6. **Given** a tenant user (any role), **When** they call the tenant routes, **Then** they get 403.

---

### User Story 2 - A tenant administrator manages staff (Priority: P1)

A tenant administrator creates staff users in their own tenant, lists them, and disables one who left. A disabled user can no longer sign in, and any session they have stops at the next refresh.

**Why this priority**: A tenant with only one person can't run a business. Disabling is the only way to remove access in this feature.

**Independent Test**: As the tenant admin, create `clerk@acme.test`, see the clerk in the list, sign in as the clerk, then disable the clerk. The clerk's refresh token is now refused, a new sign-in shows the generic error, and a `UserDisabled` message is published.

**Acceptance Scenarios**:

1. **Given** a tenant administrator, **When** they create a staff user with a free email and a valid password, **Then** the user exists in their tenant with no roles, and `UserRegistered` is published.
2. **Given** a tenant administrator, **When** they list users, **Then** they see only their own tenant's users: email, user type, disabled state, roles, creation time.
3. **Given** a staff user in the same tenant, **When** a tenant administrator disables them, **Then** the user is marked disabled, their security stamp changes, their cached permissions are dropped, and `UserDisabled` is published.
4. **Given** a disabled user, **When** they try to sign in, **Then** they see the same generic error as for a wrong password. **When** a client tries to refresh their token or start an authorization with their existing login session, **Then** it is refused.
5. **Given** a tenant administrator, **When** they try to disable themselves, **Then** the request is refused and nothing changes.
6. **Given** a user in another tenant, **When** a tenant administrator tries to disable or assign roles to them, **Then** the user is not found (404).
7. **Given** a user that is already disabled, **When** it is disabled again, **Then** the request succeeds with no change and no second `UserDisabled`.

---

### User Story 3 - A tenant administrator assigns Identity roles (Priority: P2)

A tenant administrator gives a staff user the Administrator or Viewer role in Identity, or takes it away. The change applies to that user's next request.

**Why this priority**: Without it, only the first admin can manage users. It is less urgent than creating and disabling users.

**Independent Test**: As the tenant admin, create a clerk, assign Viewer, and check with the clerk's token that `GET /users` returns 200 and `POST /users` returns 403. Unassign Viewer: `GET /users` returns 403 on the next request.

**Acceptance Scenarios**:

1. **Given** a tenant administrator and a staff user in the tenant, **When** they assign a known role, **Then** the user holds it, and the user's next request sees the new permissions.
2. **Given** the user already holds the role, **When** it is assigned again, **Then** the request succeeds with no change.
3. **Given** an unknown role name, **When** it is assigned, **Then** the request is refused with a field error.
4. **Given** a user holding a role, **When** a tenant administrator unassigns it, **Then** the user loses it on their next request.
5. **Given** a tenant administrator, **When** they try to unassign Administrator from themselves, **Then** the request is refused, so a tenant always keeps at least one administrator.
6. **Given** a disabled user, **When** a role is assigned to them, **Then** the request is refused as a conflict.

---

### User Story 4 - Other services learn about tenants and users (Priority: P2)

Identity publishes `TenantCreated`, `UserRegistered` and `UserDisabled` as integration events, defined in a versioned contracts package that other services reference. The events are saved in the same transaction as the change and sent only after it commits.

**Why this priority**: Every later service grants the tenant's first admin its own admin role on `TenantCreated` and keeps a local user table. Without the events, they can't.

**Independent Test**: A messaging test creates a tenant and disables a user, and observes exactly the expected messages with the expected fields. A test that makes the change fail after the event was produced (for example, a duplicate email caught by the database) observes no message.

**Acceptance Scenarios**:

1. **Given** a successful change, **When** it commits, **Then** its events are sent with the tenant id, user id and correlation id in the message headers.
2. **Given** a change that fails, **When** it rolls back, **Then** none of its events are sent.
3. **Given** the contracts package, **When** a service references the tagged version, **Then** it gets the three event records and nothing else from Identity.

---

### User Story 5 - A demo tenant exists in dev (Priority: P3)

In the dev stack, Identity creates a demo tenant and its admin on start, from the dev settings file, if they don't exist yet.

**Why this priority**: Developers need a tenant user to sign in with right away. It does not affect production.

**Independent Test**: Start Identity with the demo settings on an empty database: the demo tenant and admin exist and the admin can sign in. Restart: nothing is duplicated and the admin is unchanged. Start without the demo settings: no demo tenant.

**Acceptance Scenarios**:

1. **Given** demo settings and no demo tenant, **When** Identity starts, **Then** the demo tenant and its admin are created like any other tenant, events included.
2. **Given** the demo tenant exists, **When** Identity starts again, **Then** nothing changes.
3. **Given** no demo settings, **When** Identity starts, **Then** no demo tenant is created and startup succeeds.

---

### Edge Cases

- **Two requests create the same slug or email at once.** The database refuses the second, and it gets a conflict, not a server error.
- **A staff email equals the platform admin's email.** Refused: staff and platform emails share one uniqueness space.
- **Email case and spacing.** `Admin@Acme.test` and `admin@acme.test` are the same email.
- **Slug format.** Lowercase letters, digits and single hyphens, 3 to 63 characters, not starting or ending with a hyphen. Slugs can't change in this feature.
- **The route test's empty body on command routes.** A caller without the permission gets 403 before the body is validated, so missing permissions never show up as 400.
- **A disabled user still holds a 15-minute access token.** It keeps working until it expires (accepted in the constitution's token-lifetime decision). Identity's own permission cache for that user is dropped at once.
- **The platform admin calls tenant routes, or a tenant user calls platform routes.** 403, from the route categories.
- **A second platform user without the platform role.** 403 on the tenant routes. Managing platform users and their roles is out of scope.
- **Creating a staff user without the permission but with an invalid body.** 403, not 400.

## Requirements *(mandatory)*

### Functional Requirements

**Tenants**

- **FR-001**: The platform admin MUST be able to create a tenant with a slug, a name, and a first admin's email and password, in one all-or-nothing step. The tenant gets a generated, permanent identifier, which is the `tenant_id` in tokens.
- **FR-002**: Slugs MUST be unique across tenants, enforced by the database, and follow the format in Edge Cases. Names MUST be 1 to 200 characters.
- **FR-003**: The first admin MUST be a staff user of the new tenant holding the Administrator role.
- **FR-004**: The platform admin MUST be able to list tenants with slug, name and creation time, ordered by creation time, newest first.
- **FR-005**: Tenant routes MUST be platform-only and, like every route, MUST check a permission: view tenants for the list, manage tenants for creation. Platform users get these from a platform-level Identity role, Platform Administrator, assigned without a tenant. The seeded platform admin MUST hold it, including on a database created by 012. A platform user without the role gets 403. Tenants cannot be changed or disabled in this feature.

**Staff users**

- **FR-006**: A user with the manage-users permission MUST be able to create a staff user in their own tenant with an email and a password that meets the password policy from 012. The new user has no roles.
- **FR-007**: A user with the view-users permission MUST be able to list the users of their own tenant with email, user type, disabled state, Identity roles and creation time, ordered by email. The list carries action flags that combine permission and state: `canCreate` for the list (manage-users permission); per user, `canDisable` (manage-users permission, not yourself, not disabled) and `canAssignRoles` (assign-roles permission, not disabled).
- **FR-008**: A user with the manage-users permission MUST be able to disable a user of their own tenant. Disabling sets the disabled state, changes the security stamp, drops the user's cached permissions, and publishes `UserDisabled` once. Disabling yourself MUST be refused. Disabling an already disabled user succeeds with no effect.
- **FR-009**: A disabled user MUST be refused at sign-in with the generic error from 012, at authorization even with a live login session, and at refresh.
- **FR-010**: Staff and platform emails MUST be unique among all non-customer users, enforced by a database constraint on the normalized email. Customer uniqueness per tenant is added in 014.

**Identity roles**

- **FR-011**: Identity MUST define fixed roles in code. Tenant roles: Administrator (view users, manage users, view roles, assign roles) and Viewer (view users, view roles). Platform role: Platform Administrator (view tenants, manage tenants). Tenant roles can't be assigned without a tenant, and the platform role can't be assigned inside one.
- **FR-012**: A user with the assign-roles permission MUST be able to assign and unassign Identity roles for users of their own tenant. Assigning a held role or unassigning a missing one succeeds with no change. Unknown roles are a field error. Assigning to a disabled user is a conflict. Unassigning Administrator from yourself MUST be refused.
- **FR-013**: Every role change MUST invalidate the affected user's cached permissions, so the change applies to their next request.

**Events and contracts**

- **FR-014**: Identity MUST publish `TenantCreated { tenantId, adminUserId }`, `UserRegistered { userId, tenantId, userType, email }` and `UserDisabled { userId, tenantId }` through the transactional outbox: saved with the change, sent only after commit, never sent on rollback.
- **FR-015**: The event records MUST live in a packable `Identity.Contracts` project with no dependencies beyond the base library, versioned from the tag `identity-contracts/v0.1.0`.

**Validation and authorization**

- **FR-016**: Command requests MUST be validated, and invalid ones refused with 400 listing every field error. The permission check MUST happen before validation.
- **FR-017**: All logic MUST live in message handlers, permissions MUST be checked only through the permission manager, and every new route MUST pass the no-roles route test (403 for a tenant user with no roles on tenant routes, and for a platform user without access on platform routes).
- **FR-018**: Cross-tenant access to a user MUST return 404, never 403, and never reveal that the user exists.
- **FR-019**: Duplicate slug or email MUST return 409 with a problem-details body, including when two requests race.

**Dev seed**

- **FR-020**: When demo settings (slug, name, admin email, admin password) are present and no tenant has that slug, Identity MUST create the demo tenant on start, the same way as FR-001. Without demo settings it creates nothing. The dev settings file holds demo values; production has none.

### Key Entities

- **Tenant**: permanent identifier, slug (unique), name, creation time.
- **User** (extended from 012): adds the disabled state and disabled time. Staff and platform users have a globally unique normalized email.
- **Role assignment** (from 011): tenant, user, role, assigned time. Roles are fixed in code.
- **Platform role assignment**: user, role, assigned time, with no tenant.
- **Events**: `TenantCreated`, `UserRegistered`, `UserDisabled`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A platform admin creates a tenant and its admin signs in within one automated test, end to end.
- **SC-002**: 100% of duplicate slugs and emails, including concurrent duplicates, are refused with 409 and leave no partial data or events.
- **SC-003**: A disabled user's next refresh, authorization and sign-in are refused in 100% of tests.
- **SC-004**: A role change applies to the user's very next request.
- **SC-005**: Each committed change publishes exactly its events, and a rolled-back change publishes none.
- **SC-006**: The no-roles route test passes for every new route, with no 400 in place of 403.
- **SC-007**: A tenant can never be left with no active administrator through these routes.

## Assumptions

- The tenant identifier in tokens is a generated id, not the slug, so a slug change later would not break tokens or data.
- The password policy, lockout and generic sign-in error come from 012 unchanged.
- Disabled users keep their role assignments; re-enabling is out of scope.
- Lists are small in this phase, so they return all rows of a tenant without paging. Paging comes when a list needs it.
- The 15-minute access-token window after disabling is accepted, as in the constitution.
- The demo tenant uses the same creation path as any tenant, so it also publishes events.
- Customer registration, customer email uniqueness per tenant, and which user type may use which client are in 014.
