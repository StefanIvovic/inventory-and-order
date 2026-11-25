# Feature Specification: Backoffice Tenants

**Feature Branch**: `018-backoffice-tenants`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "018 backoffice-tenants: backoffice side navigation plus the Tenants page for the platform admin. Navigation entries depend on who is signed in. Tenants page for the platform admin: list of tenants (slug, name, created date, newest first) and a Create tenant button shown only when the API allows it. Create dialog with slug, name, first admin email and password, with hints; on success the list refreshes; field errors next to fields; slug-taken and email-taken on their fields; other errors in the dialog. The platform admin's home links to Tenants. Unit tests for navigation, page states and the dialog. End-to-end: the platform admin creates a tenant, sees it, and that tenant's admin can sign in. English only."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The platform admin sees all tenants (Priority: P1)

The platform admin signs in to the backoffice. A navigation panel on the side shows "Home" and "Tenants". On "Tenants" they see every tenant: its identifier, name and when it was created, newest first.

**Why this priority**: The platform admin's whole job in this system is managing tenants; until now this was only possible through the API.

**Independent Test**: Sign in as the platform admin, open "Tenants": the demo tenant is listed with its identifier, name and creation date.

**Acceptance Scenarios**:

1. **Given** the platform admin is signed in, **When** the frame shows, **Then** the navigation holds "Home" and "Tenants", and the home page links to "Tenants".
2. **Given** the Tenants page, **When** it opens, **Then** it shows a loading indicator, then a table with identifier, name and created date, newest first.
3. **Given** there are no tenants, **When** the page opens, **Then** it says "No tenants yet."
4. **Given** the list cannot be loaded, **When** the page opens, **Then** it says "Tenants could not be loaded." with "Try again".
5. **Given** a direct link to the Tenants page, **When** it is opened or reloaded, **Then** the page loads as above (after sign-in when needed).

---

### User Story 2 - The platform admin creates a tenant (Priority: P1)

The platform admin presses "Create tenant", fills in the tenant's identifier, name and the first admin's email and password, and saves. The new tenant appears at the top of the list, and its first admin can sign in to the backoffice.

**Why this priority**: Creating tenants is how every new shop starts.

**Independent Test**: Create a tenant with a new identifier; it appears at the top of the list; sign in as its first admin and see the backoffice as "Staff".

**Acceptance Scenarios**:

1. **Given** the API allows creating tenants, **When** the Tenants page shows, **Then** a "Create tenant" button is shown; **given** it does not, **then** no button is shown.
2. **Given** the create dialog, **When** it opens, **Then** it has fields for identifier, name, first admin email and first admin password, with hints: identifier "3–63 lowercase letters, digits or single hyphens", password "at least 8 characters".
3. **Given** valid values, **When** the admin saves, **Then** the dialog closes, the list reloads with the new tenant first, and a confirmation "Tenant <identifier> created." is shown.
4. **Given** the service rejects field values, **When** saving, **Then** each message is shown under its field and the dialog stays open with the values kept.
5. **Given** the identifier is already used, **When** saving, **Then** "A tenant with this identifier already exists." is shown under the identifier.
6. **Given** the first admin's email is already used by staff, **When** saving, **Then** "This email is already in use." is shown under the email field.
7. **Given** any other failure, **When** saving, **Then** a message is shown at the top of the dialog and nothing else changes.
8. **Given** a save is in progress, **When** the admin presses save again, **Then** nothing more is sent.

---

### User Story 3 - Navigation fits the person signed in (Priority: P2)

Tenant staff see only what they can use. The "Tenants" entry and page are only for the platform admin; staff opening the Tenants address see "This page isn't available to you."

**Why this priority**: The services refuse these calls anyway; showing them to staff would only produce errors.

**Independent Test**: Sign in as the demo tenant admin: no "Tenants" entry; opening `/backoffice/tenants` shows the not-available message and calls no tenant API.

**Acceptance Scenarios**:

1. **Given** tenant staff are signed in, **When** the frame shows, **Then** the navigation holds only "Home".
2. **Given** tenant staff open `/backoffice/tenants`, **When** the page loads, **Then** it shows "This page isn't available to you." and does not ask for the tenant list.

### Edge Cases

- **Session ends while the dialog is open.** Saving gets 401; the backoffice shows the sign-in screen (016 behaviour); after signing in, the admin is back on the Tenants page and the dialog is closed.
- **Identity briefly unavailable while saving.** The 016 retry banner appears; "Try again" re-sends the save; the dialog stays open meanwhile.
- **Leading/trailing spaces** in name or email are trimmed before sending; the identifier is sent lower-cased.
- **Very long tenant list.** Shown in one table, sorted newest first; paging comes when needed.
- **Narrow window.** The navigation collapses behind a menu button.

## Requirements *(mandatory)*

### Functional Requirements

**Navigation**

- **FR-001**: The frame MUST have a side navigation. For the platform admin it holds "Home" and "Tenants"; for tenant staff only "Home" (staff entries come with feature 019). The current page's entry MUST be marked.
- **FR-002**: On narrow windows the navigation MUST collapse behind a menu button in the toolbar.
- **FR-003**: The platform admin's home page MUST link to the Tenants page.

**Tenants page**

- **FR-004**: `/backoffice/tenants` MUST, for the platform admin, load the tenant list and show identifier, name and created date (local date and time), newest first, with loading, empty and error states; the error state offers "Try again".
- **FR-005**: For anyone other than the platform admin, the page MUST show "This page isn't available to you." without requesting the list.
- **FR-006**: The "Create tenant" button MUST be shown only when the list response says creating is allowed.

**Create tenant**

- **FR-007**: The create dialog MUST ask for identifier, name, first admin email and first admin password, with the hints in US2, basic checks before sending (all required, identifier rule, email format, password at least 8), and the identifier lower-cased and other text trimmed.
- **FR-008**: A successful save MUST close the dialog, reload the list and show "Tenant <identifier> created."
- **FR-009**: Field errors from the service MUST be shown under the matching fields (identifier, name, admin email, admin password); "tenant identifier taken" under the identifier and "email taken" under the email with the messages in US2; any other failure at the top of the dialog. The dialog MUST keep the values and stay open on any failure.
- **FR-010**: While a save is in progress the save button MUST be disabled.

**Tests**

- **FR-011**: Unit tests MUST cover navigation for both user types, the Tenants page states (loading, empty, list, error, not available, button by permission) and the dialog (basic checks, successful save, field errors, both conflicts, other failures, no double submit).
- **FR-012**: An end-to-end test MUST sign in as the platform admin, create a tenant with a unique identifier, see it in the list, sign out, sign in as that tenant's first admin, and see the backoffice as "Staff" without a "Tenants" entry.

### Key Entities

- **Tenant (as listed)**: identifier, name, created date; plus a list-level "may create" flag.
- **New tenant**: identifier, name, first admin email, first admin password.
- **Field error**: a field name and one or more messages from the service.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The platform admin creates a tenant from the backoffice in one dialog, and its first admin can sign in right after.
- **SC-002**: 100% of service field errors appear next to the field they belong to (unit tests).
- **SC-003**: Tenant staff never see the Tenants entry and never trigger a tenant list request.
- **SC-004**: The end-to-end tenant test passes on a fresh copy of the system in CI.

## Assumptions

- Tenants cannot be edited, disabled or deleted yet (013); the page is list and create only.
- The first admin's password is typed by the platform admin; there is no email sending.
- Users and Roles pages come in 019; until then staff navigation shows only Home.
- The tenant list endpoint, its "may create" flag and its error codes are those of 013 and do not change.
- The tenant's identifier is called "identifier" in the UI and "slug" in the API; the storefront address uses it (`/shop/<identifier>/`).
