# Endpoints: Identity Tenants and Staff

All bodies are JSON with camelCase names. Errors are problem details: 400 `validation-failed` with `errors`, 403 `permission-denied` with `missingPermissions`, 404 `not-found`, 409 with a `code`.

Order in every command handler: route category (401/403) → permission (403) → validation (400) → work (404/409).

## Platform-only

### `POST /tenants`
Permission `identity.tenants.manage`.
```json
{ "slug": "acme", "name": "Acme Ltd", "adminEmail": "admin@acme.test", "adminPassword": "..." }
```
- 201 `{ "id": "<tenant id>", "slug": "acme", "adminUserId": "<guid>" }`, `Location: /tenants/<id>`
- 409 `tenant-slug-taken`, 409 `email-taken`
- Publishes `TenantCreated` and `UserRegistered`

### `GET /tenants`
Permission `identity.tenants.view`.
- 200 `{ "items": [ { "id", "slug", "name", "createdAt" } ], "actions": { "canCreate": bool } }`, newest first

## Tenant routes

### `POST /users`
Permission `identity.users.manage`.
```json
{ "email": "clerk@acme.test", "password": "..." }
```
- 201 `{ "id": "<guid>" }`, `Location: /users/<id>`
- 409 `email-taken`
- Publishes `UserRegistered`

### `GET /users`
Permission `identity.users.view`.
- 200 `{ "items": [ { "id", "email", "userType", "disabled", "roles": [..], "createdAt", "actions": { "canDisable", "canAssignRoles" } } ], "actions": { "canCreate" } }`, ordered by email
- `canDisable`: the caller has `identity.users.manage`, the user is not the caller, and is not disabled
- `canAssignRoles`: the caller has `identity.roles.assign` and the user is not disabled

### `POST /users/{id}/disable`
Permission `identity.users.manage`.
- 204. Repeating it on a disabled user is 204 with no event
- 404 for unknown or other-tenant users, 409 `cannot-disable-self`
- Publishes `UserDisabled` and `PermissionAssignmentsChanged`

### `PUT /users/{id}/roles/{role}`
Permission `identity.roles.assign`.
- 204, also when already held
- 400 for an unknown role, 404 for unknown or other-tenant users, 409 `user-disabled`
- Publishes `PermissionAssignmentsChanged` when something changed

### `DELETE /users/{id}/roles/{role}`
Permission `identity.roles.assign`.
- 204, also when not held
- 400 for an unknown role, 404 for unknown or other-tenant users, 409 `cannot-remove-own-admin`
- Publishes `PermissionAssignmentsChanged` when something changed

### `GET /roles` (from 011)
Unchanged: tenant roles and their permissions, plus `canAssign`.
