---

description: "Task list for feature 013 Identity Tenants and Staff"
---

# Tasks: Identity Tenants and Staff

**Input**: Design documents from `specs/013-identity-tenants-staff/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. The spec's independent tests are automated integration tests. For each behaviour, write the test first and see it fail.

**Global rules**:
- No comments (C#, YAML).
- Pinned versions, tagged packages only.
- No `SaveChanges` in handlers, no `IPermissionCache` outside BuildingBlocks.
- Permission check before validation in every command handler.
- Testcontainers only.
- Build green after every task.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: BuildingBlocks 0.3.0 (blocks every command handler)

- [X] T001 Write tests first in `src/building-blocks/BuildingBlocks.Validation.Tests/CommandValidationTests.cs`: a handler that calls `Permissions.EnsureAsync` and then `ICommandValidation<T>.EnsureValidAsync` returns 403 for a caller without the permission and an invalid body, and 400 with every field error when permitted; a plain `AbstractValidator<T>` on a message still runs automatically. See them fail (research.md R3)
- [X] T002 Implement in `src/building-blocks/BuildingBlocks.Validation/`: `CommandValidator<T>`, `ICommandValidation<T>` and `CommandValidation<T>`, and the registration split in `ValidationExtensions.UseBuildingBlocksValidation` (`RegistrationBehavior.ExplicitRegistration`, own assembly scan). T001 and the 008 tests pass
- [X] T003 Run the full BuildingBlocks tests, `pack.ps1`, `check-consumer.ps1`, `check-analyzers.ps1` and the cleanup check. Commit. After the owner confirms, tag `building-blocks/v0.3.0` (local first; pushed with the PR)

---

## Phase 2: Foundational

- [X] T004 Pin BuildingBlocks 0.3.0 and add `BuildingBlocks.Validation` in `src/services/identity/Directory.Packages.props` and `Identity.Api.csproj`; run `restore-feed.ps1 -Service identity`; build green
- [X] T005 [P] Add `TenantCreated`, `UserRegistered`, `UserDisabled` records in `src/services/identity/Identity.Contracts/` per contracts/events.md (research.md R7); `Identity.Api` references the project
- [X] T006 Model in `Identity.Api`: `Tenants/Tenant.cs` (data-model.md `Tenant.Id`, `Tenant.Slug`, `Tenant.Name`, `Tenant.CreatedAt`), `Persistence/PlatformRoleAssignment.cs` (data-model.md `PlatformRoleAssignment.UserId`, `.Role`, `.AssignedAt`), and in `Users/AppUser.cs` `DisabledAt`, `CreatedAt` and `Disable(now)` (data-model.md `AppUser.DisabledAt`, `AppUser.CreatedAt`). Configure them in `Persistence/IdentityDbContext.cs`: `ux_identity_tenants_slug`, `ck_identity_tenants_slug`, and `ux_identity_users_staff_email` replacing the global email index (data-model.md `AppUser.NormalizedEmail`, research.md R4)
- [X] T007 Add migration `TenantsAndStaff` in `Persistence/Migrations/`, including the SQL that inserts a `identity.platform-administrator` row into `identity_platform_role_assignments` for every existing platform user and sets `CreatedAt` on existing users (research.md R2)
- [X] T008 `Authorization/`: new permissions, roles `identity.viewer` and `identity.platform-administrator` per data-model.md, and `IdentityPermissionProvider` reading platform assignments when the user has no tenant (research.md R2). Unit tests for the role table in `Identity.Tests/RoleTableTests.cs`
- [X] T009 `Users/HandlerUserManager.cs` (store with `AutoSaveChanges = false`, research.md R5) and `Users/AppSignInManager.cs` (`CanSignInAsync` refuses disabled users, research.md R6). In `Program.cs`, register `HandlerUserManager` and replace the default sign-in manager with `AddSignInManager<AppSignInManager>()`, so the login page uses it. Also add `Users/IdentityResultErrors.cs`, which turns a failed `IdentityResult` into a `ValidationException` on the email or password field, or 409 `email-taken` for duplicates. The disabled-login test (generic error, unchanged failed-attempt count) is in T019, once disabling exists
- [X] T010 [P] `Persistence/UniqueViolationMapper.cs`: `DbUpdateException` with Postgres `23505` on `ux_identity_tenants_slug` or `ux_identity_users_staff_email` → 409 `tenant-slug-taken` / `email-taken`; registered as `IExceptionProblemMapper` (research.md R4)
- [X] T011 `Program.cs`: `UseBuildingBlocksValidation()`, `PublishEvent<T>` for the three events, `UseBuildingBlocksPermissionInvalidation()` (research.md R7)
- [X] T012 Move `Roles/` to `Features/ListRoles/` with no behaviour change (research.md R10); `codegen.ps1 -Service identity`
- [X] T013 Test support in `Identity.Tests/IdentityAppFixture.cs`: a platform-admin token helper, `CreateTenantAsync` through the API, and a Wolverine tracked-session helper for published messages

---

## Phase 3: User Story 1 - The platform admin creates a tenant (P1)

**Goal**: platform-only tenant creation and listing.

**Independent test**: create tenant `acme`, sign in as its admin through the code flow, `GET /users` is 200, `TenantCreated` and `UserRegistered` observed.

- [X] T014 [US1] Tests first in `Identity.Tests/Tenants/CreateTenantTests.cs` and `ListTenantsTests.cs` for every acceptance scenario of US1, including the concurrent duplicate slug and duplicate email (both 409, no partial rows, no events), and a platform user without the platform role (403). See them fail
- [X] T015 [US1] `Tenants/TenantCreation.cs` domain service: rules, tenant, admin user via `HandlerUserManager` (password failures through `IdentityResultErrors` as 400 on `adminPassword`), Administrator assignment, events; sets the scoped `TenantContext` to the new tenant (research.md R8)
- [X] T016 [US1] `Features/CreateTenant/`: `CreateTenant` command, `CreateTenantValidator : CommandValidator<CreateTenant>` (data-model.md `Tenant.Slug`, `Tenant.Name`, `AppUser.Email`, `AppUser.Password`), handler (permission, validation, conflicts, `TenantCreation`, envelope tenant), endpoint `POST /tenants` `.RequirePlatform()` per contracts/endpoints.md
- [X] T017 [US1] `Features/ListTenants/`: handler and endpoint `GET /tenants` with `canCreate`
- [X] T018 [US1] `Protocol/PlatformAdminSeeder.cs`: assign `identity.platform-administrator` when creating the admin. Test in `PlatformAdminSeedTests.cs`. Migration test: migrate an empty database to the last 012 migration (`Authentication`), insert a platform user without an assignment, migrate to the latest, and check that the user holds the platform role and can call `GET /tenants`

---

## Phase 4: User Story 2 - A tenant administrator manages staff (P1)

**Goal**: create, list, disable staff; disabled users are refused everywhere.

**Independent test**: create a clerk, sign in as the clerk, disable them: refresh refused, new sign-in shows the generic error, `UserDisabled` observed.

- [X] T019 [US2] Tests first in `Identity.Tests/Users/StaffUsersTests.cs` (create and list), `DisableUserTests.cs` and `Flow/DisabledUserTests.cs` for every acceptance scenario of US2, including cross-tenant 404, self-disable 409, repeated disable with no second event, the action flags, a weak password (400 on `password`), an email already used by the platform admin (409), and two concurrent creations with the same email (one 201, one 409, one user row). See them fail
- [X] T020 [US2] `Features/CreateStaffUser/` (validator per data-model.md `AppUser.Email`, `AppUser.Password`; password policy failures through `IdentityResultErrors` as 400 on `password`), endpoint `POST /users`
- [X] T021 [US2] `Features/ListUsers/` with roles and action flags per contracts/endpoints.md, endpoint `GET /users`
- [X] T022 [US2] `Features/DisableUser/`: `Disable`, security stamp change, `UserDisabled` once, `PermissionAssignmentsChanged`; endpoint `POST /users/{id}/disable`
- [X] T023 [US2] Disabled checks in `Connect/ConnectEndpoints.cs` (authorize and refresh) next to the lockout check (research.md R6)

---

## Phase 5: User Story 3 - Identity roles (P2)

**Goal**: assign and unassign Identity roles with immediate effect.

**Independent test**: assign Viewer, `GET /users` 200 and `POST /users` 403 for the clerk; unassign, `GET /users` 403 on the next request.

- [X] T024 [US3] Tests first in `Identity.Tests/Users/RoleAssignmentTests.cs` for every acceptance scenario of US3, including removing your own Administrator (409), assigning to a disabled user (409), and assigning `identity.platform-administrator` inside a tenant (400). See them fail
- [X] T025 [US3] `Features/AssignRole/`: assign and unassign handlers (validator per data-model.md `RoleAssignment.Role`), endpoints `PUT` and `DELETE /users/{id}/roles/{role}`, `PermissionAssignmentsChanged` only when something changed

---

## Phase 6: User Story 4 - Events and contracts (P2)

- [X] T026 [US4] Tests in `Identity.Tests/Events/OutboxTests.cs`: headers (`tenant-id` of the new tenant for `TenantCreated`), and a forced failure after the events were produced sends nothing. See them fail where the behaviour is missing, then fix
- [X] T027 [US4] `pack.ps1` produces `Identity.Contracts` with no dependencies; a test in `src/services/identity/Identity.Tests/ContractsPackageTests.cs` asserts the assembly references only the base library

---

## Phase 7: User Story 5 - Demo tenant in dev (P3)

- [X] T028 [US5] Tests first in `Identity.Tests/Tenants/DemoTenantSeedTests.cs`: created once with events, unchanged on restart, absent without settings. See them fail
- [X] T029 [US5] `Tenants/DemoTenantSeeder.cs` with `IDbContextOutbox<IdentityDbContext>` (research.md R8), called from `Persistence/StartupTasks.cs`; `Identity:DemoTenant:*` settings; `DEMO_TENANT_SLUG`, `DEMO_TENANT_NAME`, `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` in `deploy/dev.env` and `src/services/identity/compose.yaml`

---

## Phase 8: Polish

- [X] T030 `NoRolesRouteTests.cs` and `OwnTokenTests.ProtocolAndLoginEndpointsAreAllowListed`: all 8 non-allow-listed routes (7 new plus `GET /roles`) checked, none skipped, no 400 (SC-006)
- [X] T031 `codegen.ps1 -Service identity` and commit the generated code
- [ ] T032 Run all BuildingBlocks and Identity tests, the pin checks and the cleanup check; run quickstart.md sections 2–5; scan the new files for comments
- [ ] T033 Commit. After the owner confirms, push the branch and the tags `building-blocks/v0.3.0` and `identity-contracts/v0.1.0`, open the PR, and confirm `building-blocks.yml`, `identity.yml` and `deploy.yml` pass

---

## Dependencies

- Phase 1 → T004
- T004 → T005–T013 (T005 and T010 in parallel with the others)
- US1 needs Phase 2. US2 needs US1 (tenants and their admins). US3 needs US2. US4 needs US1 and US2. US5 needs US1
- Polish last

## Parallel Opportunities

- T005 ∥ T010
- Test files within a story are separate files and can be written in parallel

## Implementation Strategy

- MVP: Phases 1–3. A platform admin creates a tenant whose admin signs in.
- Then staff, roles, events, the demo seed.
- One PR. Owner confirmations: the 0.3.0 tag, then the push with both tags and the PR.
