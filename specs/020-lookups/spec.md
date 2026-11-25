# Feature Specification: Lookups

**Feature Branch**: `020-lookups`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "020 lookups: no enums as types in the database or in APIs. Each service keeps its value lists in its own database in two tables: lookup types (id, name, code) and lookup values (id, name, code, type id). Each service exposes lookup endpoints and seeds, on startup, only the values it needs to work. Owner decisions: the rule covers only values stored in the database or exposed in APIs (internal code enums stay); any signed-in user may read lookups (tenant staff, customers, the platform admin); lookups are global per service, not per tenant; Identity's user type (staff, customer, platform) moves to a lookup as the first real case, and a staff-status lookup is added for the 019 staff list filter. Constitution amendment to v1.6.0."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A screen reads a value list from the service that owns it (Priority: P1)

A signed-in user's screen (backoffice or storefront) needs the options for a list, such as the kinds of user or the staff statuses. It asks the service that owns that data for its lookups and gets every value with a stable code and a display name. Nothing about the options is hard-coded in the screen.

**Why this priority**: This is what the rule is for. Option lists live in the owning service's data, not in code copied into every client.

**Independent Test**: Sign in as tenant staff, as a customer and as the platform admin. Each can read the values of Identity's `user-type` and `staff-status` lists, with ids, codes and names. Without signing in, the same requests are refused.

**Acceptance Scenarios**:

1. **Given** a signed-in user, **When** they ask for the values of `user-type`, **Then** they get `customer` (Customer), `platform` (Platform) and `staff` (Staff), each with its id, code and name, sorted by name.
2. **Given** a signed-in user, **When** they ask for the values of `staff-status`, **Then** they get `active` (Active) and `disabled` (Disabled).
3. **Given** an unknown type code, **When** its values are asked for, **Then** the answer is "not found".
4. **Given** no sign-in, **When** any lookup is asked for, **Then** the request is refused as unauthenticated.
5. **Given** a client that already has the values, **When** it asks again with its stored version marker, **Then** the answer says "not modified" without sending the values again.
6. **Given** a search box over a type's values, **When** text is typed, **Then** only values whose name or code contains that text (ignoring case) come back, at most the requested number, sorted by name, with a token for the next page when there are more.
7. **Given** a select that already holds chosen values, **When** it opens with those values' ids or codes, **Then** exactly those values come back, so their names can be shown without loading the whole list.
8. **Given** the screen knows only the list it needs, **When** it asks for values, **Then** it names the list by its permanent type code (for example `staff-status`), never by a database id.

---

### User Story 2 - A service starts with the values it needs, every time (Priority: P1)

When a service starts, against an empty database or one that already has data, it makes sure every lookup type and value it depends on exists, with the same ids in every environment. It adds what is missing and corrects changed display names. It never removes values, because stored data may refer to them. If something the service depends on is still missing after seeding, the service refuses to start.

**Why this priority**: Stored records point at lookup values, so the values must exist before the service handles any request.

**Independent Test**: Start Identity on an empty database, and on a database where one value's name was changed and an extra value was added by hand. Both times the defined values match the service's definitions, with the same ids, and the extra value is untouched. Starting twice adds nothing new.

**Acceptance Scenarios**:

1. **Given** an empty database, **When** the service starts, **Then** every lookup type and value it defines exists, with its fixed id.
2. **Given** a database where a value's name differs from the definition, **When** the service starts, **Then** the name is corrected and the id is unchanged.
3. **Given** a database with an extra value the service does not define, **When** the service starts, **Then** that value is left alone.
4. **Given** the service has already started once, **When** it starts again, **Then** no rows are added or changed.
5. **Given** two copies of the service starting at the same time, **When** both seed, **Then** each value exists once and both copies start.

---

### User Story 3 - Identity stores a user's type as a reference to a lookup value (Priority: P1)

Identity stores each user's type (staff, customer, platform) as a reference to a `user-type` lookup value, not as free text. Existing users keep their type through the change. Everything users do keeps working: signing in, tokens, tenants, staff and customer registration.

**Why this priority**: The owner chose this as the first real case; it proves the rule on stored data.

**Independent Test**: Upgrade a database that already holds a platform admin, staff and customers. Every user keeps their type, the database rejects a type that is not a `user-type` value, and the full Identity test suite still passes.

**Acceptance Scenarios**:

1. **Given** an existing database with users, **When** the upgrade runs, **Then** every user refers to the `user-type` value matching their previous type, and nothing else about them changes.
2. **Given** the upgraded database, **When** a user row refers to something that is not a `user-type` value, **Then** the database refuses it.
3. **Given** the existing rules (a platform user has no tenant; staff and customers have one; staff emails are unique; a customer's email is unique within its tenant), **When** users are created, **Then** the same rules hold as before.
4. **Given** a user signs in, **When** a token is issued, **Then** it still carries the type as its code (`staff`, `customer` or `platform`), and the staff list and the registration message still show the code.

---

### Edge Cases

- **Code change**: codes are permanent. Renaming means a new display name, never a new code. A code changed in a definition counts as a new value, and the old one stays.
- **Name edited in the database by hand**: the next start puts back the defined name.
- **Seeding and the schema upgrade**: seeding runs after the upgrade, never before.
- **Lookups of another service**: each service serves only its own; there is no shared lookup store.
- **Tenant users**: lookups are the same for every tenant. Reading them never depends on, or leaks, tenant data.
- **List size**: most lists are short, but a list may grow (for example countries or units). Values come a page at a time (default 50, at most 200), so a search or select stays fast whatever the size.
- **Filters that match nothing**: an empty page, not an error. Unknown ids or codes in a "these values" request are simply absent from the answer.

## Requirements *(mandatory)*

### Functional Requirements

**Rule and scope**

- **FR-001**: Values that are stored in a database or exposed by an API as a fixed set of choices MUST come from the owning service's lookup tables, not from code enums or unchecked free text. Enums used only inside code stay allowed.
- **FR-002**: Each service MUST keep its lookups in its own database: lookup types (id, name, code) and lookup values (id, name, code, type). A code MUST be unique among types, and among the values of one type. Lookups are the same for every tenant.

**Seeding**

- **FR-003**: Each service MUST define in its code the lookup types and values it needs to work, each with a fixed id, and seed them on every start, after the schema upgrade and before it accepts requests. Seeding MUST add what is missing and correct display names, MUST NOT delete anything, and MUST be safe to run repeatedly and by several copies at once.
- **FR-004**: A service MUST refuse to start if, after seeding, a lookup value it refers to in code is missing.
- **FR-005**: Only values the service needs to function are seeded; demo or sample data is not lookup data.

**Reading lookups**

- **FR-006**: Each service MUST offer one read, open to any signed-in user (tenant staff, customer or platform admin) and refused without sign-in: the values of one type, addressed by the type's permanent code (id, code, name, sorted by name), or "not found" for an unknown code. Clients MUST NOT need a type's database id. Types themselves are not listed by any API: they only group values, and screens know the codes of the lists they use.
- **FR-006a**: The values read MUST support, in any combination:
  - **search**: values whose name or code contains the given text, ignoring case
  - **these values**: only the values with the given ids, or the given codes (up to 200), for showing what a select already holds
  - **paging**: a page size (1 to 200, default 50) and a token for the next page, with the same cost at any depth

  Invalid filters (an oversized page, too many ids, a malformed token) MUST be refused as invalid input. These filters are what a later shared search, select and multi-select control in the screens relies on.
- **FR-007**: Lookup answers MUST carry a version marker and allow short client caching. A request with the current marker MUST get "not modified" without the values.
- **FR-008**: Opening lookups to any signed-in user MUST be a distinct, reviewed route category. The route check that every service runs MUST report these routes separately, and MUST prove they refuse callers who are not signed in.

**Identity**

- **FR-009**: Identity MUST define the lookup types `user-type` (values `staff`, `customer`, `platform`) and `staff-status` (values `active`, `disabled`).
- **FR-010**: Identity MUST store each user's type as a reference to a `user-type` value. The database MUST reject a reference to any other value. Existing users MUST keep their type through the upgrade, with no other change.
- **FR-011**: All existing Identity rules and outward behaviour MUST stay the same: the type rules for tenants, the email uniqueness rules, sign-in, tokens carrying the type code, the staff list showing the type code, and the user-registered message carrying the type code.

**Governance**

- **FR-012**: The constitution MUST state the rule (FR-001), the lookup route category (FR-008) and the seeding rule (FR-003 to FR-005), as version 1.6.0.

### Key Entities

- **Lookup type**: a named list of choices owned by one service. Id, code (permanent, unique), name (display).
- **Lookup value**: one choice in a list. Id (fixed in every environment), code (permanent, unique within its type), name (display), and the type it belongs to.
- **User (Identity)**: refers to a `user-type` value instead of holding the type as text.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Each of the three kinds of signed-in user reads Identity's lookups successfully, and an anonymous caller is refused, in automated tests.
- **SC-002**: A repeated lookup request with the current version marker returns "not modified" with no body.
- **SC-003**: Starting Identity on an empty database, on an upgraded database and twice in a row leaves exactly the defined types and values with the same ids each time.
- **SC-004**: Upgrading a database that has users of all three types keeps every user's type, and the whole existing Identity test suite passes with unchanged behaviour.
- **SC-005**: The developer's existing local data survives the upgrade without a reset.
- **SC-006**: No value stored in Identity's database or exposed by its API as a fixed choice is defined only as a code enum or free text.

## Assumptions

- The two tables carry exactly the columns the owner listed. Values are sorted by name, so no sort column is needed.
- Codes are lower-case words joined by hyphens, as role and permission names already are.
- Display names are English only, like the rest of the system. Translations are a later feature.
- Nobody edits lookups through the system in this feature. An admin screen for lookups is out of scope.
- The shared search, select and multi-select screen control is a later feature; this feature provides the reads it needs.
- Screens refer to lists by type code, which is a permanent part of the service's API, like a route name.
- The shared lookup machinery (tables, seeding, reading) lives in the shared infrastructure packages. The lists themselves belong to each service.
- Tokens, the registration message and existing API fields keep carrying codes, so no consumer has to change.
- The staff-status lookup is first used by the 019 staff list filter, which resumes after this feature.
- Other services (inventory, orders) don't exist yet and adopt lookups when they are built.
