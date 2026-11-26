# Feature Specification: App-Wide Loading Indicator

**Feature Branch**: `022-loading-indicator`

**Created**: 2026-11-26

**Status**: Draft

**Input**: User description: "022 loading-indicator: an app-wide loader in the backoffice. One thin bar under the toolbar shows whenever the app is waiting for the server, so every action (saving a role, disabling a user, paging, filtering) gives feedback. Owner decisions: pages keep their own first-load states (loading, error, try again) and the lookup panel keeps its loading row; the global bar also shows while dialogs save; background loads (prefetching the next page, lookup scroll pages) do not show it."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See that the app is working (Priority: P1)

A staff member clicks something that asks the server, such as Next, a filter, Disable or a role switch. A thin bar under the toolbar shows the app is working until the answer arrives. Quick answers don't make it flicker.

**Why this priority**: Without it, actions like disabling a user or saving a role give no feedback until they finish.

**Independent Test**: On the Users page with a slow server, change a filter: the bar appears and disappears when the list arrives. With a fast server, nothing flickers.

**Acceptance Scenarios**:

1. **Given** a request that takes longer than 200 ms, **When** it is running, **Then** the bar shows under the toolbar, and it hides when no request is running.
2. **Given** a request that finishes within 200 ms, **When** it ends, **Then** the bar never showed.
3. **Given** several requests running together, **When** some finish, **Then** the bar stays until the last one ends, including failed or cancelled requests.
4. **Given** the bar is showing, **When** a screen reader user is on the page, **Then** the bar is announced as "Loading".

---

### User Story 2 - Background loads stay quiet (Priority: P1)

Loads the user did not ask for (the next Users page fetched in advance, more lookup options while scrolling, the user-type names in the toolbar, the session check) do not show the bar, so the page does not flash while the user is reading.

**Why this priority**: The owner wants the bar to mean "your action is running", not every network call.

**Independent Test**: Open the Users page with more than one page of users: after the first page shows, the next page is fetched in the background and the bar does not appear.

**Acceptance Scenarios**:

1. **Given** a request marked as background, **When** it runs, **Then** the bar does not show for it.
2. **Given** a background request and a user request running together, **When** the user request ends, **Then** the bar hides even though the background request is still running.

---

### User Story 3 - Waits that are not a single request (Priority: P2)

Some waits are not one request, for example a dialog save that waits for the service and then closes. Code can mark any such wait so the bar shows for its whole length.

**Why this priority**: Dialog saves should show the bar (owner decision), and future flows may chain several steps.

**Independent Test**: Saving the Add staff user dialog with a slow server shows the bar until the dialog closes.

**Acceptance Scenarios**:

1. **Given** a marked wait, **When** it runs longer than 200 ms, **Then** the bar shows until it ends, whether it succeeds or fails.

---

### Edge Cases

- **Pages' first load**: the page still shows its own loading, error and "Try again" states. The global bar may show at the same time; it does not replace them.
- **The Users page's in-page bar for reloads** (filters, paging) is removed, because the global bar now covers it; its first-load state stays.
- **The lookup panel** keeps its "Loading options" row; its option requests are background for the global bar, because the panel already shows progress.
- **Sign-in service unavailable**: a request held for retry is still running, so the bar stays until the user retries or leaves; the retry banner explains why.
- **Signed out or no session**: the bar is part of the frame, so it only exists while signed in.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The backoffice MUST show one thin, indeterminate bar directly under the toolbar while at least one user-visible wait is running, and hide it when none is.
- **FR-002**: The bar MUST appear only after a wait has lasted 200 ms, and MUST NOT flicker between back-to-back waits (it stays while the next wait starts within the same moment).
- **FR-003**: Every request the backoffice sends MUST count as a wait unless it is marked as background; ending in success, failure or cancellation all end the wait.
- **FR-004**: Background marking MUST be available per request; the next-page prefetch, lookup option pages and names, the toolbar's user-type names and the session check MUST be marked.
- **FR-005**: Code MUST be able to mark any asynchronous step as a wait; the Add staff user, Create tenant and Roles dialogs' saves MUST show the bar.
- **FR-006**: The bar MUST be announced to assistive technology as "Loading" while visible, and be hidden from it when not.
- **FR-007**: Pages' own first-load states and the lookup panel's loading row MUST stay unchanged; the Users page's separate reload bar MUST be removed.

### Key Entities

- **Wait**: anything the user is waiting for: a service request not marked as background, or a marked asynchronous step.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every user action that asks the server and lasts over 200 ms shows the bar; none under 200 ms does (unit tests with fake timers).
- **SC-002**: Prefetching and lookup paging never show the bar (unit tests).
- **SC-003**: The bar never stays visible after all waits ended, including failures and cancellations (unit tests).
- **SC-004**: The end-to-end tests still pass.

## Assumptions

- Backoffice only; the storefront gets its own copy of the small service when it needs one (no shared UI library, 016).
- 200 ms is the threshold; it can be tuned later in one place.
- No percentage progress; the bar is indeterminate.
- The bar sits under the toolbar and above the side navigation and content, full width.
