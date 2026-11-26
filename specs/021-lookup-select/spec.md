# Feature Specification: Lookup Select

**Feature Branch**: `021-lookup-select`

**Created**: 2026-11-26

**Status**: Draft

**Input**: User description: "021 lookup-select: a shared component in the backoffice that serves as search, select and select multiple over any lookup list, using the lookups endpoint from 020 (search, ids, codes, paging). Screens name the list by its type code and never hold the options. First use: the Users page status filter (019)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pick one value from a lookup list (Priority: P1)

A staff member opens a field or filter backed by a lookup list, sees the options with their display names, types to narrow them, and picks one. The field then shows the chosen value's name, and the form or filter receives its value.

**Why this priority**: Single choice is the common case and replaces hand-made selects.

**Independent Test**: On the Users page, the Status filter is this component over `staff-status`. It shows "All" plus the list's values, and picking "Disabled" filters the list exactly as before.

**Acceptance Scenarios**:

1. **Given** a field over a list, **When** it opens, **Then** it shows the first values by name, sorted as the service returns them.
2. **Given** the open field with text typed, **When** the user presses Enter or the search button, **Then** only matching values are shown (the service's search), and "No matches" when none. Typing alone never searches.
3. **Given** a value is picked, **When** the field closes, **Then** it shows that value's name, and the form receives the value's code (or its id, when the field is set up for ids).
4. **Given** an optional field with a value, **When** the user clears it, **Then** the value becomes empty and the field shows its empty label (for example "All").
5. **Given** a form that already holds a value, **When** the field first shows, **Then** it displays that value's name, fetched by its code or id alone, without loading the whole list.

---

### User Story 2 - Pick several values (Priority: P1)

For a field that takes several values, the user picks them one after another from the same searchable list. Each chosen value shows as a removable chip, and values already chosen are marked in the list. Search works the same way as in single choice: on Enter or the search button.

**Why this priority**: The owner asked for select multiple as part of the component.

**Independent Test**: A test host form with a multiple field over `user-type`: picking "Staff" and "Customer" gives two chips and the form value `['staff', 'customer']`; removing a chip removes it from the value; a form starting with two values shows both chips by name.

**Acceptance Scenarios**:

1. **Given** a multiple field, **When** the user picks values, **Then** each appears as a chip with its name, in the order picked, and the form receives the list of codes (or ids).
2. **Given** chips, **When** the user removes one (click or Backspace on an empty input), **Then** it leaves the value.
3. **Given** a value already chosen, **When** the list shows it again, **Then** it is marked as chosen and picking it again does not add a duplicate.
4. **Given** a form starting with values, **When** the field first shows, **Then** all their names show as chips after one request for those codes or ids.

---

### User Story 3 - Long lists stay fast (Priority: P2)

For a list with thousands of values, the field never loads the whole list. It shows the first page, loads the next page when the user scrolls to the end of the options, and searches on the server when the user asks for it.

**Why this priority**: Lists will grow (for example products or cities), and the endpoint caps a page at 200 values (owner decision: never everything at once).

**Independent Test**: With a test list answering pages of 50 and a next-page token, opening the field asks for one page; scrolling to the end asks for the next page with the token; a search (Enter or the button) restarts at the first page of the search.

**Acceptance Scenarios**:

1. **Given** a list with more values than one page, **When** the user scrolls to the end of the options, **Then** the next page is appended, and nothing more is asked once the last page is reached.
2. **Given** a search was started, **When** another search starts before the first answer arrives, **Then** the earlier answer is ignored.
3. **Given** the search text was emptied, **When** the user presses Enter or the button, **Then** the full list shows again from its first page.

---

### Edge Cases

- **The list cannot be loaded** (error or unknown list): the field shows "Options could not be loaded." with "Try again" inside the panel. A chosen value's name that cannot be fetched shows as "Unavailable", never the raw code (no fallback to codes, owner rule).
- **A chosen value no longer exists**: it shows as "Unavailable" and can be removed.
- **Disabled or read-only form fields**: the field cannot be opened or changed.
- **Required fields**: the form's own required rule applies; the field shows the form's error messages under it like other fields.
- **Keyboard**: arrows move through options; Enter picks the highlighted option, and runs the search when no option is highlighted; Escape closes; Backspace on an empty input removes the last chip in multiple mode.
- **Sign-in service unavailable**: the existing retry banner handles it; the field keeps waiting.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The backoffice MUST have one reusable lookup field that takes a list's type code and shows that list's values by display name. Screens MUST NOT hold the options.
- **FR-002**: It MUST support single choice and multiple choice, chosen by the screen.
- **FR-003**: The value given to the form MUST be the value's code by default, or its id when the screen asks for ids; a list of them in multiple mode; empty when nothing is chosen.
- **FR-004**: Search MUST run on the server only when the user presses Enter (with no option highlighted) or the search button, restarting at the first page; typing alone MUST NOT send a request; stale answers MUST be ignored. Owner decision, 2026-11-26.
- **FR-005**: The options MUST load a page at a time, and the next page only when the user reaches the end of the options; the whole list is never requested at once.
- **FR-006**: Names of values already in the form MUST be fetched by their codes or ids in one request per 200 values, without loading the list.
- **FR-007**: The field MUST work as a Signal Forms field (value, touched, disabled, read-only, required and errors from the form) and as a plain input/output for filters.
- **FR-008**: Load failures MUST be shown in the panel with "Try again"; codes are never shown in place of names.
- **FR-009**: The Users page Status filter MUST use this field over `staff-status`, with "All" as the empty choice, and behave as in 019.
- **FR-009a**: The Users page "Search email" box MUST follow the same rule: it searches on Enter or its search button only, never while typing, restarting at the first page with the total. Clearing the text and searching shows all users again. Owner decision, 2026-11-26.
- **FR-010**: The field MUST be usable with the keyboard and announce itself to screen readers as a combobox with a list of options.

### Key Entities

- **Lookup field**: a type code, single or multiple, code or id as the value, an optional empty label, and a label.
- **Lookup value**: id, code, name, as served by the lookups endpoint (020).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: No screen in the backoffice holds a list of lookup options; the Status filter is served by the component.
- **SC-002**: Opening a field over a list of any size makes one request; showing a form with N chosen values makes ⌈N/200⌉ extra requests (one for up to 200).
- **SC-003**: Unit tests cover single, multiple, search, paging, preset values, errors, keyboard and Signal Forms binding.
- **SC-004**: The end-to-end staff test still passes with the new Status filter and the explicit email search.
- **SC-005**: No search in the backoffice sends a request while the user is typing.

## Assumptions

- The component lives in the backoffice only. The storefront gets its own copy when it first needs one (016 decision: two independent apps, no shared UI library).
- The lists come from Identity's lookups endpoint today. The service base address is an input with Identity as the default, so later services can be used without changing the component.
- The role filter stays a plain select over `/roles`, because roles are not lookups (019 R2).
- Angular Material's autocomplete and chips are the building blocks; no new third-party package.
