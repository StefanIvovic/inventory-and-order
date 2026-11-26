# Feature Specification: Backoffice Staff

**Feature Branch**: `019-backoffice-staff`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "019 backoffice-staff: backoffice screens for tenant staff: a Users page (list, Add staff user, disable with confirmation, role checkboxes) and a read-only Roles page. Owner addition: standard pagination for the staff list that is extremely fast at any size, with filtering by email, status and role (cursor pagination in Identity, one database query per page). Controls follow the API's action flags only. Unit tests and an end-to-end test: a tenant admin adds a staff user, gives them Viewer, the user sees the list without management controls, and after being disabled can no longer sign in."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Staff see who works in their tenant (Priority: P1)

A staff member opens "Users" in the backoffice and sees everyone on their tenant's staff: email, roles, whether the account is active, and when it was created.

**Why this priority**: Every other staff action starts from this list.

**Independent Test**: Sign in as the demo tenant admin, open "Users": the admin is listed as "Administrator", "Active".

**Acceptance Scenarios**:

1. **Given** tenant staff are signed in, **When** the frame shows, **Then** the navigation holds "Home", "Users" and "Roles".
2. **Given** the Users page, **When** it opens, **Then** it shows a loading indicator, then a table with email, roles ("Administrator", "Viewer", or "No roles"), status ("Active" or "Disabled") and created date, sorted by email.
3. **Given** a staff member without permission to view users, **When** the page opens, **Then** it says "You don't have access to users."
4. **Given** the list cannot be loaded, **When** the page opens, **Then** it says "Users could not be loaded." with "Try again".
5. **Given** the platform admin opens the Users address, **When** the page loads, **Then** it says "This page isn't available to you." without asking for the list.

---

### User Story 1b - Staff page through and filter a large staff list quickly (Priority: P1)

With hundreds or thousands of staff, the Users page shows one page at a time (25 by default, 10 or 50 on request), with "Previous" and "Next", the total number of matching users, and filters: an email search (matching the start of the address), status (all, active, disabled) and role (any, Administrator, Viewer). Every page appears without noticeable delay, the first or the hundredth alike, and "Next" is instant because the following page is already loaded.

**Why this priority**: The owner requires standard pagination that stays extremely fast however many staff a tenant has.

**Independent Test**: With 50,000 staff in a tenant, page forward many times and filter by email start, status and role: each page answer takes one database query using the staff index, and a deep page is no slower than the first.

**Acceptance Scenarios**:

1. **Given** more staff than the page size, **When** the Users page opens, **Then** it shows the first page sorted by email, "1–25 of N users", "Previous" disabled and "Next" enabled.
2. **Given** a page, **When** "Next" is pressed, **Then** the following page shows at once (it was loaded in advance), and "Previous" returns to the page before.
3. **Given** a page size change, **When** 10, 25 or 50 is chosen, **Then** the list restarts at its first page with that size.
4. **Given** the email search, **When** text is typed, **Then** after a short pause the list restarts at its first page showing only users whose email starts with that text (case-insensitive), with the total updated.
5. **Given** the status or role filter, **When** a value is chosen, **Then** the list restarts at its first page with only matching users and the total updated; filters combine.
6. **Given** no user matches, **When** the filters apply, **Then** "No users match these filters." is shown with a "Clear filters" action.
7. **Given** a changed or tampered page token, **When** it is sent, **Then** the service refuses it and the page restarts from the first page.

---

### User Story 2 - An administrator adds a staff user (Priority: P1)

An administrator presses "Add staff user", enters the new person's email and a first password, and saves. The person appears in the list with no roles, and can sign in.

**Why this priority**: The tenant's staff can't grow without it; until now only the API could do it.

**Independent Test**: As the demo admin, add `clerk@demo.test`; the list shows them with "No roles"; they can sign in to the backoffice.

**Acceptance Scenarios**:

1. **Given** the list says adding is allowed, **When** the page shows, **Then** an "Add staff user" button is shown; otherwise not.
2. **Given** the add dialog, **When** it opens, **Then** it asks for email and first password, with the hint "at least 8 characters".
3. **Given** valid values, **When** saving, **Then** the dialog closes, the list reloads with the new person, and "Staff user <email> added." is shown.
4. **Given** an email already used by staff, **When** saving, **Then** "This email is already in use." is shown under the email.
5. **Given** the service rejects a field, **When** saving, **Then** its message is shown under the field and the dialog stays open with the values.

---

### User Story 3 - An administrator manages roles (Priority: P1)

For each staff member the administrator can turn "Administrator" and "Viewer" on and off. Each change takes effect right away and the list shows the new roles.

**Why this priority**: A new staff member has no roles and can do nothing until given one.

**Independent Test**: Give `clerk@demo.test` "Viewer": their row shows "Viewer"; signing in as them shows the Users list without management buttons.

**Acceptance Scenarios**:

1. **Given** a row whose roles may be changed, **When** the administrator opens "Roles" on it, **Then** a dialog shows "Administrator" and "Viewer" as switches reflecting the person's current roles; rows that may not be changed have no "Roles" action.
2. **Given** the roles dialog, **When** a switch is turned on or off, **Then** the change is saved immediately, the switch is disabled while saving, and the row shows the new roles.
3. **Given** the administrator turns off their own "Administrator", **When** saving, **Then** "You can't remove your own Administrator role." is shown and the switch goes back on.
4. **Given** the person was disabled meanwhile, **When** a switch changes, **Then** "This user is disabled." is shown and the switch goes back.
5. **Given** any other failure, **When** a switch changes, **Then** "The role could not be changed." is shown and the switch goes back.

---

### User Story 4 - An administrator disables a staff user (Priority: P1)

The administrator chooses "Disable" on a row, confirms, and the person can no longer sign in or use their session.

**Why this priority**: Removing access when someone leaves is basic safety.

**Independent Test**: Disable `clerk@demo.test`; their row shows "Disabled"; signing in as them fails with the generic sign-in error.

**Acceptance Scenarios**:

1. **Given** a row that may be disabled, **When** the page shows, **Then** it has a "Disable" action; the administrator's own row and disabled rows have none.
2. **Given** "Disable" is chosen, **When** the confirmation "Disable <email>? They will lose access to the backoffice." is confirmed, **Then** the account is disabled, the list reloads, and "<email> disabled." is shown; cancelling changes nothing.
3. **Given** the service refuses (own account, or other failure), **When** confirming, **Then** a message is shown and the list is unchanged.

---

### User Story 5 - Staff see what each role allows (Priority: P2)

"Roles" in the navigation shows each tenant role with the permissions it grants, in plain words.

**Why this priority**: Administrators need to know what they're granting; it's read-only.

**Independent Test**: Open "Roles": "Administrator" lists "View users, Manage users, View roles, Assign roles"; "Viewer" lists "View users, View roles".

**Acceptance Scenarios**:

1. **Given** the Roles page, **When** it opens, **Then** each role is shown by its display name with its permissions by display name.
2. **Given** a staff member without permission to view roles, **When** it opens, **Then** "You don't have access to roles." is shown.
3. **Given** the platform admin opens it, **When** the page loads, **Then** "This page isn't available to you." is shown without a request.

### Edge Cases

- **New staff member with no roles** signs in: the navigation shows Users and Roles, and each page says they don't have access (the service answers 403).
- **A role added later in Identity**: it appears in the filter, the roles dialog and the Roles page with its display name, with no backoffice change.
- **Administrator removes their own Viewer role**: allowed (they keep Administrator).
- **Session ends during a dialog**: the dialog closes and the sign-in screen shows (018 behaviour).
- **Identity briefly unavailable while saving**: each dialog shows its own retry line (018 behaviour), including the roles dialog, where the switch waits until "Try again".
- **Many staff**: paged, sorted by email; see US1b.
- **A user changes while paging** (added, disabled, renamed): pages after the current token reflect the change; nothing is skipped or shown twice for rows that keep their email.

## Requirements *(mandatory)*

### Functional Requirements

**Navigation**

- **FR-001**: For tenant staff the navigation MUST hold "Home", "Users" and "Roles"; for the platform admin it stays "Home" and "Tenants".

**Users page**

- **FR-002**: `/backoffice/users` MUST, for tenant staff, load the staff list and show email, roles (display names from the roles list; "No roles" when none), status (the `staff-status` lookup name) and created date, sorted by email, with loading, error ("Try again") and no-access (403) states. For the platform admin it MUST show the not-available text without requesting.
- **FR-003**: "Add staff user" MUST be shown only when the list says adding is allowed; "Disable" and "Roles" per row only when that row's flags allow them.

**Paging and filters (Identity and backoffice)**

- **FR-011**: The staff list service MUST return one page at a time, sorted by email then id, with a page size from 1 to 100 (default 25) and an opaque token for the next page (none on the last page). Paging MUST cost the same at any depth: one database query per page, served by an index, never skipping rows.
- **FR-012**: The staff list service MUST filter by email start (case-insensitive), status (a `staff-status` lookup code, `active` or `disabled`; none means all, per constitution v1.6.0) and role (a tenant role); filters combine. An unknown status, an unknown role or a page size outside 1–100 MUST be refused as invalid input; a malformed page token MUST be refused as a rule violation.
- **FR-013**: The total of matching users MUST be computed only when asked for, in one extra query.
- **FR-014**: The backoffice MUST show "Previous"/"Next", "<from>–<to> of <total> users", a page size choice (10, 25, 50), the email search (applied after a short pause), a status filter whose options ("All" plus the values of the `staff-status` lookup) come from Identity's lookup endpoint, a role filter whose options ("Any" plus the tenant roles) come from the roles list, and "No users match these filters." with "Clear filters"; any filter or size change restarts at the first page and asks for the total once. It MUST load the next page in advance so "Next" is instant, and "Previous" MUST reuse the page tokens already seen.
- **FR-015**: Tenant isolation, the per-row and list flags, and the staff-only rule MUST be unchanged by paging and filters.
- **FR-016**: An Identity integration test MUST seed 50,000 staff and prove, from the query plan, that a deep page and each filter use the staff index without a sequential scan, and that one page costs exactly one database command (permission lookups excluded).

**Add staff user**

- **FR-004**: The add dialog MUST ask for email (required, email format, at most 256) and first password (required, at least 8), send the email trimmed, show service field errors under their fields and "This email is already in use." for an email conflict, and on success close, reload the list and confirm.

**Roles**

- **FR-005**: The roles dialog MUST show one switch per tenant role from the roles list, in its order and with its display names, set from the row's current roles, save each change immediately (assign on, remove off), disable the switch while saving, revert it and show the messages in US3 on failure, and update the list when closed.

**Disable**

- **FR-006**: "Disable" MUST ask for confirmation, then disable the account, reload the list and confirm; refusals show a message and change nothing.

**Roles page**

- **FR-007**: `/backoffice/roles` MUST show each tenant role with its permissions, using the display names from the roles list, with loading, error, no-access and not-available states.
- **FR-008**: The backoffice MUST hold no list of roles, permissions, statuses or other choices. Every list and every display name comes from the API: role and permission display names from `GET /roles` (built from Identity's role definitions), status names from the `staff-status` lookup. There is no fallback to codes: if a needed list cannot be loaded, the page shows its error state with "Try again".
- **FR-008a**: `GET /roles` MUST return, per role, its name and display name, and per permission its name and display name, all defined in Identity's code next to the roles and permissions.

**Tests**

- **FR-009**: Unit tests MUST cover the Users page states and flags, the add dialog (checks, success, field errors, conflict), the roles dialog (assign, remove, each refusal, revert), disable (confirm, cancel, refusal) and the Roles page; fixtures MUST use problem bodies in the shape the services actually send (018 R7).
- **FR-010**: An end-to-end test MUST: sign in as the demo tenant admin, add a staff user with a unique email, give them Viewer; sign in as that user and see the Users list without "Add staff user", "Roles" or "Disable"; sign in as the admin again and disable them; and check the disabled user's sign-in fails.

### Key Entities

- **Staff user (as listed)**: id, email, user type, disabled, roles, created date; per-row flags "may disable" and "may change roles"; list flag "may add".
- **Role**: name and its permissions; list flag "may assign".
- **New staff user**: email and first password.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator adds a staff user and gives them a role in under a minute, entirely in the backoffice.
- **SC-002**: A Viewer sees no management control anywhere (unit tests and end-to-end).
- **SC-003**: A disabled staff user can't sign in right after being disabled.
- **SC-004**: The end-to-end staff test passes on a fresh copy of the system in CI.
- **SC-005**: With 50,000 staff in one tenant, any page and any filter combination is answered with one database query using the index, and page 2,000 is no slower than page 1 (integration test).
- **SC-006**: "Next" shows the following page with no visible wait in the backoffice (it is loaded in advance).

## Assumptions

- The staff APIs and their flags are those of 013, except the staff list, which gains paging and filters in this feature (owner decision). The Tenants list (018) gets the same paging in a later feature.
- There is no "enable" in the API, so disabling is one-way for now.
- Passwords are typed by the administrator; there is no email or invitation yet.
- Customers never appear in this list (014).
- Role changes affect the person's permissions within about a minute at most (permission cache invalidation, 007); the list itself updates immediately.
