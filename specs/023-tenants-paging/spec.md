# Feature Specification: Tenants Paging

**Feature Branch**: `023-tenants-paging`

**Created**: 2026-11-27

**Status**: Draft

**Input**: User description: "023 tenants-paging: the platform admin's Tenants list in the backoffice pages instead of loading every tenant. Owner decisions: order by identifier A–Z (keyset paging like the 019 Users list, page sizes 10/25/50/100 with 25 default, next page prefetched in the background, Previous/Next); one search box that matches the start of the identifier or the start of the name, case-insensitive, run only on Enter or the search button (explicit search); an optional total asked for on the first page and after a search change, shown as "1–25 of N tenants". The page keeps its first-load, error and "Try again" states and the Create tenant action; after creating a tenant the list restarts at the first page with the total. An invalid page token restarts at the first page. The service caps the page size at 100 and never returns everything at once."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse tenants page by page (Priority: P1)

The platform admin opens Tenants and sees the first 25 tenants in identifier order, with "1–25 of N tenants". They move with Next and Previous, and can show 10, 25 or 50 per page, as on the Users page.

**Why this priority**: Today the page asks for every tenant at once. With many tenants it becomes slow, and the owner's rule is that no list returns everything at once.

**Independent Test**: With 60 tenants, open Tenants: 25 show, sorted by identifier, with "1–25 of 60 tenants". Next shows 26–50, then 51–60, where Next is disabled. Previous goes back.

**Acceptance Scenarios**:

1. **Given** more tenants than the page size, **When** the admin opens Tenants, **Then** the first page shows in identifier order with the range and the total, and Previous is disabled.
2. **Given** the first page is shown, **When** the admin presses Next, **Then** the next page shows at once if it was already fetched in the background, and the range moves on.
3. **Given** the last page is shown, **When** it is the end of the list, **Then** Next is disabled.
4. **Given** a page other than the first, **When** the admin presses Previous, **Then** the previous page shows without asking the service again.
5. **Given** any page, **When** the admin changes the page size, **Then** the list restarts at the first page with the total.
6. **Given** no tenants at all, **When** the admin opens Tenants, **Then** "No tenants yet." shows.

---

### User Story 2 - Find a tenant (Priority: P1)

The platform admin types the start of an identifier or a name and presses Enter or the search button. The list shows only the tenants whose identifier or name starts with that text, ignoring case.

**Why this priority**: Paging alone makes one tenant among many hard to reach.

**Independent Test**: With tenants `acme` ("Acme Ltd") and `globex` ("Acme Holdings"), search `acme`: both show. Search `GLO`: only `globex` shows. Typing alone sends nothing.

**Acceptance Scenarios**:

1. **Given** text in the search box, **When** the admin types, **Then** nothing is searched until Enter or the search button.
2. **Given** a search, **When** it runs, **Then** the list restarts at the first page with the total, showing tenants whose identifier or name starts with the trimmed text, ignoring case.
3. **Given** a search with no matches, **When** it runs, **Then** "No tenants match this search." shows with a way to clear the search.
4. **Given** an empty search box, **When** the admin runs the search, **Then** all tenants show again.

---

### User Story 3 - Create a tenant and see it (Priority: P2)

After creating a tenant, the list restarts at the first page with the total and a confirmation, as today.

**Why this priority**: The create flow already exists; it must keep working with paging.

**Independent Test**: Create a tenant: the dialog closes, a confirmation shows, and the list reloads at the first page with the total increased by one.

**Acceptance Scenarios**:

1. **Given** the admin is on any page or search, **When** a tenant is created, **Then** the list restarts at the first page with the total, keeping the current search and page size.

---

### Edge Cases

- **Invalid page token** (for example after a service restart or a tampered address): the list restarts at the first page with the total, without an error.
- **Tenants created while paging**: pages never repeat or skip an existing tenant; a new tenant may or may not appear, depending on where it sorts.
- **Search text longer than a name can be**: the service refuses it; the page shows its normal error.
- **Page size outside 1–100 asked for directly**: the service refuses it.
- **The prefetch fails**: nothing is shown; Next asks for the page when pressed.
- **Not the platform admin**: the page stays unavailable and asks for nothing, as today.
- **First load fails**: the page shows "Tenants could not be loaded." and "Try again", as today.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The service MUST return tenants one page at a time, in identifier order, with a token for the next page when there is one, and MUST NOT return more than 100 tenants in one answer.
- **FR-002**: The page size MUST be 1–100 on the service, and 10, 25 (default) or 50 on the page, the same as the Users page.
- **FR-003**: The service MUST return the number of matching tenants only when asked; the page MUST ask for it on the first page and whenever the search, page size or list restarts.
- **FR-004**: The service MUST filter by a search text that matches the start of the identifier or the start of the name, ignoring case and surrounding spaces; an empty text MUST mean no filter.
- **FR-005**: The page MUST run the search only on Enter or its search button, never while typing.
- **FR-006**: The page MUST show the range as "from–to of N tenants" when the total is known, and "from–to" otherwise.
- **FR-007**: The page MUST fetch the next page in the background after showing a page, and MUST NOT show the app-wide loading bar for it.
- **FR-008**: Previous MUST show pages already seen without asking the service again; Previous MUST be disabled on the first page and Next on the last.
- **FR-009**: An invalid page token MUST make the service answer with a distinct error, and the page MUST then restart at the first page with the total.
- **FR-010**: After a tenant is created, the list MUST restart at the first page with the total, keeping the search and page size.
- **FR-011**: The page MUST keep its first-load, error and "Try again" states, its Create tenant action (shown only when the service allows it), and its unavailable state for anyone but the platform admin.
- **FR-012**: The service MUST keep the tenants list platform-only and keep its permission check.

### Key Entities

- **Tenant list page**: a page of tenants (identifier, name, created), the next-page token when there is one, the total when asked for, and whether the caller may create tenants.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: No answer from the tenants list ever holds more than 100 tenants.
- **SC-002**: Moving to the next page shows it without waiting when it was prefetched.
- **SC-003**: Walking every page from first to last shows each existing tenant exactly once.
- **SC-004**: A search shows only tenants whose identifier or name starts with the text, ignoring case, in every tested case.
- **SC-005**: The existing end-to-end tests and the no-roles route test still pass.

## Assumptions

- The Tenants page follows the Users page (019) for paging, page sizes, total, prefetch and the explicit search (021).
- Identifiers are unique, so identifier order is stable.
- The search text is at most 200 characters, the longest a name can be.
- Search matches only the start of the identifier or name; a "contains" search is out of scope (trigram follow-up).
- The list stays sorted by identifier only; no other sort or filter is added.
- Backoffice only; the storefront has no tenants list.
