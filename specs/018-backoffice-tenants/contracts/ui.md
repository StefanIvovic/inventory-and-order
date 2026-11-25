# Contract: Tenants screens

## Navigation

| User type | Entries |
|---|---|
| `platform` | Home (`/backoffice/`), Tenants (`/backoffice/tenants`) |
| `staff` | Home |

- **Active entry**: the current page's entry is highlighted.
- **Narrow windows** (handset breakpoint): the panel is hidden, and a "Menu" button in the toolbar opens it as an overlay.

## Home

- Platform admin: "Welcome to the backoffice." and a link "Manage tenants" to `/backoffice/tenants`.
- Staff: "Welcome to the backoffice."

## Tenants page (`/backoffice/tenants`)

| State | Content |
|---|---|
| not platform | "This page isn't available to you." |
| loading | progress bar |
| error | "Tenants could not be loaded." with "Try again" |
| loaded, empty | heading "Tenants", the create button if allowed, "No tenants yet." |
| loaded | heading "Tenants", "Create tenant" (only if `canCreate`), and a table: Identifier, Name, Created |

## Create tenant dialog

| Field | Label | Hint |
|---|---|---|
| `slug` | Identifier | 3–63 lowercase letters, digits or single hyphens |
| `name` | Name | |
| `adminEmail` | First admin email | |
| `adminPassword` | First admin password (type password) | At least 8 characters |

- **Buttons**: "Cancel" and "Create". "Create" is disabled while saving or while the form is invalid.

Errors:

| Cause | Where | Text |
|---|---|---|
| required field empty | field | "Required." |
| identifier rule | identifier | "Use 3–63 lowercase letters, digits or single hyphens." |
| email format | admin email | "Enter a valid email address." |
| password too short | admin password | "At least 8 characters." |
| name too long | name | "At most 200 characters." |
| email too long | admin email | "At most 256 characters." |
| retry needed (503 `identity-unavailable`) | top of dialog | "Sign-in service is unavailable." with "Try again" |
| 400 `validation-failed` | each field in `errors` | the service's messages |
| 409 `tenant-slug-taken` | identifier | "A tenant with this identifier already exists." |
| 409 `email-taken` | admin email | "This email is already in use." |
| other failure | top of dialog | "The tenant could not be created. Please try again." |

- **On success**: the dialog closes, the list reloads, and a snack bar shows "Tenant <identifier> created."
- **When the session ends** (state leaves `signed-in`): every open dialog closes, so the sign-in screen is not covered.
