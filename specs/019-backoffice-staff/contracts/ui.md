# Contract: Staff screens

## Navigation

- Staff: Home, Users (`/backoffice/users`), Roles (`/backoffice/roles`).
- Platform admin: Home, Tenants (unchanged).

## Users page

| State | Content |
|---|---|
| platform admin | "This page isn't available to you." (no request) |
| loading | progress bar |
| 403 | "You don't have access to users." |
| error | "Users could not be loaded." with "Try again" |
| loaded | heading "Users", "Add staff user" (only if `canCreate`), a filter bar, the table (Email, Roles, Status, Created, Actions), and a paging bar |
| loaded, nothing matches | "No users match these filters." with "Clear filters" |

**Filter bar**:
- "Search email" (text, applied on Enter or its search button; changed in 021)
- "Status": the 021 lookup select over `staff-status`, with "All" as the empty choice
- "Role": Any, then the roles from `/roles` by display name

**Paging bar**:
- "Rows per page": 10, 25 (default), 50
- the range text "<from>–<to> of <total> users"
- "Previous", disabled on the first page
- "Next", disabled when there is no next page

Any filter or size change restarts at the first page.

- **Row actions**: "Roles" (only if `canAssignRoles`) and "Disable" (only if `canDisable`).

## Add staff user dialog

| Field | Label | Hint |
|---|---|---|
| `email` | Email | |
| `password` | First password (type password) | At least 8 characters |

Messages:

| Cause | Where | Text |
|---|---|---|
| empty | field | "Required." |
| email format | email | "Enter a valid email address." |
| email too long | email | "At most 256 characters." |
| password too short | password | "At least 8 characters." |
| 400 `validation-failed` | each field in `errors` | the service's messages |
| 409 `email-taken` | email | "This email is already in use." |
| retry needed | top | "Sign-in service is unavailable." with "Try again" |
| other | top | "The staff user could not be added. Please try again." |

- **On success**: the dialog closes, the list reloads, and a snack bar shows "Staff user <email> added."

## Roles dialog

- **Title**: "Roles for <email>". It has one switch per role from `/roles`, labelled with its display name, and a "Close" button.
- **Saving**: each switch saves on change and is disabled while saving.

| Refusal | Text in the dialog | Switch |
|---|---|---|
| 409 `cannot-remove-own-admin` | "You can't remove your own Administrator role." | back on |
| 409 `user-disabled` | "This user is disabled." | back |
| other | "The role could not be changed." | back |
| retry needed (503 `identity-unavailable`) | "Sign-in service is unavailable." with "Try again" | stays disabled until retried |

- **On close**: if anything changed, the list reloads.

## Disable

- **Confirmation**: title "Disable user", text "Disable <email>? They will lose access to the backoffice.", buttons "Cancel" and "Disable".
- **On success**: the list reloads, and the snack bar shows "<email> disabled."
- **409 `cannot-disable-self`**: the snack bar shows "You can't disable your own account."
- **Other failures**: the snack bar shows "The user could not be disabled."

## Roles page

| State | Content |
|---|---|
| platform admin | "This page isn't available to you." |
| loading | progress bar |
| 403 | "You don't have access to roles." |
| error | "Roles could not be loaded." with "Try again" |
| loaded | heading "Roles", and per role its display name as a heading with its permissions' display names as a list |
