# Research: Backoffice Tenants

Sources on 2026-11-25:
- angular.dev: Signal Forms overview, form submission, validation (read)
- material.angular.dev: client-rendered, so it couldn't be read
- the installed Angular Material 22.3 and CDK type definitions in `src/web/backoffice/node_modules`
- the Identity 013 source for the API contract

## R1. Forms: Signal Forms

- **Decision**: Signal Forms (`@angular/forms/signals`) with Material's `matInput`, `mat-form-field` and `mat-error`.
  - `form(model, schema)` with `required`, `email`, `minLength`, `maxLength` and `pattern`, each with a `message`.
  - `[formField]` on the inputs.
  - `submit(form, action)` to save; the action returns field-targeted errors for the service's answers.
- **Rationale**: angular.dev recommends Signal Forms for new apps built with signals, and the backoffice is one (zoneless, signals throughout).
- **Verified on 2026-11-25** with throwaway tests in a copy of the backoffice, on Angular and Material 22.3. Material's docs are client-rendered and silent on this, so the tests were the evidence:
  - `<input matInput [formField]="f.email">` binds both ways.
  - An invalid, touched field puts `mat-form-field` into the error state (`mat-form-field-invalid`), and `mat-error` shows the message from `errors()`.
  - Errors returned from the `submit()` action with `fieldTree: f.slug` appear in `f.slug().errors()`, leave the other fields untouched, and clear when the field is edited.
  - `f().submitting()` is true while the action runs.
- **Rejected**: Reactive Forms. They would work too, but they are the older API for a signal-based app. The first draft of this plan chose them only because Material's support was undocumented, and the tests settled that.

## R2. Layout: side navigation

- **Structure**: `MatSidenavModule` in `Shell`: `mat-sidenav-container`, a `mat-sidenav` holding a `mat-nav-list` (`MatListModule`, `a mat-list-item routerLink routerLinkActive`), and the content area.
- **Responsive**: `BreakpointObserver.observe(Breakpoints.Handset)` from `@angular/cdk/layout`, converted with `toSignal`.
  - Handset: `mode="over"`, closed by default, with a menu button in the toolbar.
  - Otherwise: `mode="side"`, opened.
  - The installed CDK declares both.
- **Entries**: a computed list from the session's `userType`:
  - `platform`: Home, Tenants
  - `staff`: Home
- **Icons**: none, because there is no icon font (016). The menu button is a text button with `aria-label="Menu"`.

## R3. Tenants API (Identity 013, through the gateway)

| Call | Answer |
|---|---|
| `GET /api/identity/tenants` | 200 `{ items: [{ id, slug, name, createdAt }], actions: { canCreate } }` (platform only; others 403) |
| `POST /api/identity/tenants` `{ slug, name, adminEmail, adminPassword }` | 201 `{ id, slug, adminUserId }` |
| | 400 with `code: "validation-failed"` and `errors: { slug?, name?, adminEmail?, adminPassword?: string[] }` (camelCase names from `FieldNames.ToClientName`) |
| | 409 with `code` `tenant-slug-taken` or `email-taken` |

Service problems carry their code in `code`; `type` is the RFC 9110 section URL. Gateway problems (015) carry their code in `type`. See R7.

`TenantsApi`, a small injectable, wraps both calls. The 016 interceptor adds `X-Bff-App` and handles 401 and 503.

## R4. Error mapping in the dialog

- **400 `validation-failed`**: each `errors[key]` becomes `{ kind: 'server', message, fieldTree }` on the field with that name, returned from the `submit()` action. Keys without a field go to the top error.
- **409**:
  - `tenant-slug-taken` returns "A tenant with this identifier already exists." on `fieldTree: f.slug`.
  - `email-taken` returns "This email is already in use." on `fieldTree: f.adminEmail`.
- **Anything else**, except 401 and 503, which the interceptor handles: the top error "The tenant could not be created. Please try again."
- **Problem bodies**: a helper `problemOf(error)` reads `{ code, errors }` safely from `HttpErrorResponse.error`. The code is `code` when present and `type` otherwise, which covers both service and gateway problems.

## R5. List page

- **State**: a signal `loading | error | loaded(items, canCreate)`.
- **Sorting**: by `createdAt`, newest first, in the page; the API's order isn't relied on.
- **Dates**: `DatePipe` `medium`, in local time.
- **Not available**: when the session user isn't `platform`, the page renders the not-available message and never calls the API.
- **Create**:
  - `MatDialog.open(CreateTenantDialog)`, which returns the created slug on success
  - the page then reloads and shows `MatSnackBar` "Tenant <slug> created."
  - `disableClose` while saving
- **Lint**: angular-eslint's template accessibility rules are already on (016), so the table gets column headers and the dialog gets labels.

## R6. End-to-end

`tests/e2e/specs/backoffice-tenants.spec.ts`:
1. Sign in as `PLATFORM_ADMIN_EMAIL` / `PLATFORM_ADMIN_PASSWORD` (from `dev.env`) and open Tenants.
2. Create `e2e-<timestamp>` with first admin `admin@e2e-<timestamp>.test`.
3. See it as the first row, then sign out with the confirmation.
4. Sign in as the new admin and see "Staff" and no "Tenants" link.

Unique identifiers keep the test independent of other runs. The stack is fresh per run anyway.

## R7. Implementation findings

- **Problem code location (found on the real stack).** BuildingBlocks problems put the error code in `code`, next to `type: "https://tools.ietf.org/html/rfc9110#section-…"`. R3 had assumed `type`.
  - The unit tests passed anyway, because their fake answers used the same wrong shape. The real-browser check (creating `demo` showed the generic message) found it.
  - `problemOf` now reads `code` first and falls back to `type`, which gateway problems use.
  - The test fixtures were rewritten from real answers captured through the gateway. Later screens should copy fixtures from real answers, not from the plan.
- **Signal Forms owns `name`.** `[formField]` sets the input's `name` itself, and an explicit `name` attribute is a compile error (NG8022). Tests find inputs by `data-field`.
- **Signal Forms timing in tests.** A field error returned from the `submit()` action appears after a few promise turns. The dialog tests' `settle()` runs several `whenStable` and macrotask rounds before asserting. With a single round, the conflict tests failed even though the browser showed the errors.
- **TestBed overrides** (`MatDialogRef`, `BreakpointObserver`) must come before `setUpSession()`, which injects. `TestBed.resetTestingModule()` is used where one test needs a different override.
- **Dialogs close on sign-out.** The `Shell` closes all dialogs when it is destroyed, which happens as soon as the session leaves `signed-in`. Material removes a closing dialog from `openDialogs` only after its close animation, so the test asserts the `closeAll` call and the dialog's closing state.
- **Bundle budget.** With the side navigation, menu and toolbar, the initial bundle is 623 kB raw (about 144 kB transferred), above Angular's default 500 kB warning budget but below the 1 MB error. The Tenants page (table, dialog, forms) is lazy-loaded as its own 177 kB chunk. The budget was not raised; that is an owner decision.
- **Accessible names overlap.** "Menu" and "User menu", and "Tenants" and "Manage tenants", both match as substrings. Playwright locators use `exact: true`.

