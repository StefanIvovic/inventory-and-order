# Research: Backoffice Staff

This builds on 018: Signal Forms dialogs, `problemOf` reading `code` first, dialogs closing on sign-out, lazy routes, and test fixtures in the real problem shape. The API contract below was read from the Identity 013 source on 2026-11-25.

## R1. Staff API (through the gateway, `/api/identity`)

| Call | Success | Refusals |
|---|---|---|
| `GET /users?q&status&role&size&after&includeTotal` (new in this feature, R9) | 200 `{ items: [{ id, email, userType, disabled, roles: string[], createdAt, actions: { canDisable, canAssignRoles } }], nextCursor, total?, actions: { canCreate } }` | 403 without `identity.users.view`; 400 invalid filters; 422 `invalid-cursor` |
| `POST /users` `{ email, password }` | 201 `{ id }` | 400 `validation-failed` (`errors.email`, `errors.password`); 409 `email-taken` |
| `POST /users/{id}/disable` | 204 | 409 `cannot-disable-self`; 404 unknown or customer |
| `PUT /users/{id}/roles/{role}` | 204 | 409 `user-disabled`; 400 for an unknown role |
| `DELETE /users/{id}/roles/{role}` | 204 | 409 `cannot-remove-own-admin`, `user-disabled` |
| `GET /roles` | 200 `{ roles: [{ name, permissions: string[] }], canAssign }` | 403 without `identity.roles.view` |

- **Flags**: `canDisable` is false for your own row and for disabled rows, and `canAssignRoles` is false for disabled rows, both computed by Identity.
- **Problem shape**: codes come in `code` (018 R7), and the 403 problem from BuildingBlocks lists the missing permissions.

## R2. Display names come from the API (owner, 2026-11-26)

The backoffice keeps no lists of choices and no label maps.
- **Roles and permissions**: `GET /roles` (Identity) returns display names defined in code next to `IdentityRoles` and `IdentityPermissions`:
  ```json
  { "roles": [ { "name": "identity.administrator", "displayName": "Administrator",
                 "permissions": [ { "name": "identity.users.view", "displayName": "View users" } ] } ],
    "canAssign": true }
  ```
  Roles are sorted by display name, and so are permissions. A new role appears in the filter, the roles dialog and the Roles page without a UI change.
- **Statuses**: `GET /lookups/staff-status/values` (020).
- **Why not a `tenant-role` lookup**: roles are authorization code (principle VI). A lookup would be a second copy needing a sync test, and BuildingBlocks would have to allow `.` in codes. Rejected with the owner.
- **No fallback**: the Users page loads the staff page, `/roles` and the status lookup together. If either list fails, the page shows "Users could not be loaded." with "Try again"; a 403 on `/roles` counts as no-access.
- **Page sizes** (10, 25, 50) stay in the UI: they are a screen setting, not data.

## R3. Roles dialog

- **Switches**: `MatSlideToggle`, one per role in the `/roles` answer, in its order, labelled with its display name. The dialog receives the roles from the page.
- **On change**: `PUT` or `DELETE` the role.
  - While the request runs, the toggle is disabled.
  - On failure, the toggle goes back and the code's message shows in the dialog.
  - On success, the dialog's local roles update.
- **Closing**: the dialog returns `true` when anything changed, and the page then reloads.
- **Rejected**: assigning roles in the table with immediate toggles. That crowds the table; a dialog per row keeps one place for refusal messages.

## R4. Disable confirmation

- **Dialog**: a small `ConfirmDialog` (`MatDialog`) with title, text and the "Disable" and "Cancel" buttons.
- **On confirm**: the page posts the disable.
  - Success reloads and shows the snack bar "<email> disabled.".
  - `cannot-disable-self` shows "You can't disable your own account."
  - Anything else shows "The user could not be disabled."
- **Messages**: refusal messages use the snack bar, because the confirmation is already closed.

## R5. Add staff user

The same Signal Forms pattern as 018's tenant dialog:
- fields `email` (required, format, at most 256; sent trimmed) and `password` (required, at least 8)
- `submit()` maps 400 field errors and the 409 `email-taken` conflict onto the fields
- the dialog has its own retry line
- fixtures copy the real answers

## R6. Pages and routes

- **Lazy routes**: `users` and `roles` (`loadComponent`), like `tenants`.
- **Page states**: `not-available` (platform admin), `loading`, `no-access` (403), `error`, and `loaded`.
- **Sorting**: users by email, case-insensitive.
- **Navigation** (018 `Shell`): staff get Home, Users and Roles.

## R7. End-to-end

`tests/e2e/specs/backoffice-staff.spec.ts`:
1. Sign in as `DEMO_ADMIN_EMAIL`, open Users, and add `clerk-<timestamp>@demo.test`.
2. Open its Roles and switch on Viewer, then close.
3. Sign out and sign in as the clerk. Users lists them as "Viewer", with no "Add staff user", "Roles" or "Disable".
4. Sign out and sign in as the admin, then disable the clerk.
5. Sign out and try to sign in as the clerk: Identity's generic error, and the browser stays on the login page.

## R9. Staff list paging and filters (Identity)

Owner requirement: standard pagination, extremely fast at any size, filtering by two or three fields. Options weighed with the owner:
- **Page numbers** (offset and limit): rejected. A count on every page, and deep pages scan the rows they skip.
- **PostgreSQL function**: rejected. Same plan, so no speed gain. It would bypass the EF global tenant filter (principle V) and move logic out of handlers (principle III).
- **Cursor pagination**: chosen.

**Query**

- **Shape**: `ListUsers(Q, Status, Role, Size = 25, After, IncludeTotal)`, validated after the permission check: `Size` 1–100, `Q` up to 256 characters, `Status` null or a `StaffStatuses` code (`active`, `disabled`, from the 020 catalog), `Role` a tenant role.
- **Filters**: staff only; `NormalizedEmail` starts with `Q` upper-cased; `Status` `active` means `DisabledAt IS NULL` and `disabled` means `DisabledAt IS NOT NULL`; `Role` is an `EXISTS` on `role_assignments`.
- **Cursor**: `After` is decoded into `(NormalizedEmail, Id)` and applied as `(email > e) OR (email = e AND id > i)`.
- **Order and size**: `ORDER BY NormalizedEmail, Id` with `LIMIT Size + 1`, so the extra row tells whether a next page exists.

**One database command per page**

Each user's roles are projected inside the same query, as a correlated subquery that EF Core puts into the same statement. Permission checks normally cost no query: the set is resolved once per request and cached per user for 60 seconds by BuildingBlocks `PermissionCache`.

**Total**

Only with `IncludeTotal=true`: one `COUNT(*)` with the same filters and no cursor.

**Cursor format**

Base64url JSON of `(NormalizedEmail, Id)`. It is opaque to clients, and a malformed one is a 422 `invalid-cursor` (`DomainErrorKind.Rule`).

**Indexes (migration)**

- `ix_identity_users_staff_list` on `(tenant_id, normalized_email, id)`, filtered to `user_type = 'staff'`, with `INCLUDE (email, disabled_at, created_at)` so the page can be read from the index alone.
- `ix_role_assignments_role_user` on `(tenant_id, role, user_id)`, for the role filter and the role projection.
- **Collation**: the email column in the index uses collation `"C"`, and the query compares and orders with `EF.Functions.Collate(..., "C")`, so prefix matching and ordering both use the index. Normalized emails are upper-case, so byte order is a stable sort.
- *Verify* in the plan test: the EF translation of the collated comparison, and whether the migration builder can set the index collation. Otherwise use raw `CREATE INDEX` SQL in the migration.

**Proof**

- `StaffListPagingTests` (Identity.Tests) seeds 50,000 staff with a bulk insert in its own Testcontainers database.
- It asserts, through `EXPLAIN`, an index scan on `ix_identity_users_staff_list` and no `Seq Scan`, for a deep page and for each filter.
- An EF command interceptor counts exactly one command per page request, after the permission set is warm.
- The test also covers filters, size bounds, the last page (no `nextCursor`), a tampered cursor (422), `includeTotal`, unchanged flags, other tenants' users never appearing, and customers never appearing.

**Codegen**

The endpoint signature changes, so the Wolverine generated code is regenerated with `scripts/codegen.ps1 -Service identity`, and its CI check must pass.

## R10. Backoffice paging

- **State**: the page component keeps `filters { q, status, role }`, `size`, a stack of visited `after` tokens, and a small cache of pages keyed by `filters + size + after`.
- **Previous**: pops the stack and shows the cached page, or requests it again.
- **Next**: shows the prefetched page.
- **Prefetch**: after each page is shown, the next page is fetched in the background when `nextCursor` exists.
- **Filter changes**: any change clears the stack and the cache and requests the first page with `includeTotal=true`. The email search waits 300 ms after typing stops.
- **Bad cursor**: a 422 `invalid-cursor` restarts at the first page.
- **Range label**: computed from the page index and the size, for example "26–50 of 1,234 users". Without a total, it shows just the range.
- **Filter options** (constitution v1.6.0, no hard-coded choices):
  - Status: "All" plus `GET /api/identity/lookups/staff-status/values`, read with the page by a small `lookups/lookups-api.ts` service. The browser revalidates it with the ETag.
  - Role: "Any" plus the roles from `GET /api/identity/roles`, by display name (R2).
  - The row's status is shown with the lookup name, and roles with their display names. No fallback to codes (R2).
- **Controls**: Material `mat-select` for the size, status and role, a `matInput` for the search, and plain buttons for Previous and Next. `MatPaginator` is not used, because it assumes page numbers and a known length.

