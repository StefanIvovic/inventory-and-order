# Research: Identity Tenants and Staff

## R1. Tenant identity

- **Decision**: `Tenant.Id` is a generated GUID string (`N` format, 32 characters, within `TenantFilter.TenantIdMaxLength`). It is the `tenant_id` in tokens and in every tenant-owned row. The slug is a separate unique column used only for display and, in 014, in storefront URLs.
- **Rationale**: Slugs may become editable later. Tokens and data in every service must not change when they do.
- **Alternatives considered**: slug as id (breaks on rename), integer id (leaks tenant count).

## R2. Platform permissions

- **Finding**: Constitution principle VI requires the no-roles route test to call platform routes as a platform user with no roles and expect 403. `IdentityPermissionProvider` returns nothing for users without a tenant, and `RoleAssignment` is tenant-owned, so the tenant filter and the save interceptor reject rows without a tenant.
- **Decision**: a separate `PlatformRoleAssignment` entity (`identity_platform_role_assignments`: user id, role, assigned time; key user id plus role), not tenant-owned. `IdentityRoles` gains `identity.platform-administrator` with `identity.tenants.view` and `identity.tenants.manage`. The provider reads `PlatformRoleAssignment` when the user has no tenant and `RoleAssignment` otherwise. `PlatformAdminSeeder` assigns the role when it creates the admin. The migration inserts the assignment for every existing platform user, so databases created by 012 keep working.
- **Alternatives considered**: a nullable tenant on `RoleAssignment` (fights the tenant filter and interceptor); granting platform users permissions implicitly (fails the route test and principle VI).

## R3. Permission check before validation

- **Spike (2026-11-24)**: in BuildingBlocks.Validation.Tests, a message with a failing validator and a handler `BeforeAsync` that throws: `IMessageBus.InvokeAsync` threw `ValidationException` for both a static and an instance handler. Wolverine's FluentValidation middleware runs before any handler code, including `Before` methods. Ordering inside the handler cannot fix it.
- **Decision**: BuildingBlocks 0.3.0, additive, in BuildingBlocks.Validation:
  - `CommandValidator<T> : AbstractValidator<T>`: the base class for validators of HTTP commands.
  - `ICommandValidation<T>.EnsureValidAsync(T command, CancellationToken)`, implemented by `CommandValidation<T>` over `IEnumerable<CommandValidator<T>>` and registered as an open generic: runs every validator for `T` and throws `ValidationException` with all failures. It is generic because Wolverine's code generation refuses handler dependencies on `IServiceProvider` (found while implementing T002). The existing `ValidationProblemMapper` turns it into 400 with every field error.
  - `UseBuildingBlocksValidation()` switches Wolverine to `UseFluentValidation(RegistrationBehavior.ExplicitRegistration)` and scans the application assembly itself: plain `AbstractValidator<T>` types are registered as `IValidator<T>` (automatic middleware, unchanged for messages), `CommandValidator<T>` types only as `CommandValidator<T>`, so the middleware never sees them.
  - Tests: a command with a `CommandValidator` gets 403 from a handler that checks permissions first, and 400 with all errors when permitted. The existing 008 tests stay green.
- **Handler shape**: the handler takes `ICommandValidation<TCommand>`; `await Permissions.EnsureAsync(...)`, then `await validation.EnsureValidAsync(command, ct)`, then the work.
- **Alternatives considered**: a permission middleware policy before validation (a declarative permission check, forbidden by principle VI); letting the route test accept 400 (contradicts FR-016 and the 011 carry-over).

## R4. Uniqueness and 409

- **Decision**:
  - `identity_users`: drop the unique index on `NormalizedEmail`; add `ux_identity_users_staff_email` unique on `NormalizedEmail` `WHERE "UserType" <> 'customer'`. The `NormalizedUserName` unique index stays until 014 changes user names for customers.
  - `identity_tenants`: unique `ux_identity_tenants_slug` on `Slug`, plus a check constraint for the slug format.
  - Handlers check first and throw `DomainException(..., Conflict)` with codes `tenant-slug-taken` and `email-taken`.
  - A race reaches the database: Identity registers `UniqueViolationMapper : IExceptionProblemMapper` for `DbUpdateException` with Postgres `23505`, mapping those two constraint names to 409 with the same codes. Other unique violations stay 500.
- **Rationale**: the user asked for database constraints. The check gives a clean message on the common path; the constraint guarantees correctness under races.

## R5. UserManager inside handlers

- **Finding**: `UserManager.CreateAsync` and `UpdateAsync` call `SaveChanges` through the store when `AutoSaveChanges` is true. Inside a Wolverine handler that would bypass the single transaction and the IAO1002 rule in spirit.
- **Decision**: handlers use `HandlerUserManager`, a scoped `UserManager<AppUser>` subclass built over its own `UserOnlyStore` instance with `AutoSaveChanges = false` on the same scoped `IdentityDbContext`. It normalizes, validates and hashes passwords, and changes the security stamp, but never saves; Wolverine's transactional middleware saves once. The default `UserManager<AppUser>` keeps autosave for code outside handlers: the login page and `SignInManager` (lockout counts), and the startup seeders.
- **Check**: a test where a handler fails after `CreateAsync` leaves no user row; the 012 login and lockout tests stay green.
- **Alternatives considered**: writing users without `UserManager` (duplicates hashing and normalization rules).

## R6. Disabled users

- **Decision**: `AppUser.DisabledAt` (nullable). `AppUser.Disable(now)` sets it once and returns whether it changed. The handler then calls `UserManager.UpdateSecurityStampAsync`.
  - Login page: `AppSignInManager` overrides `SignInManager<AppUser>.CanSignInAsync` to refuse disabled users. `PasswordSignInAsync` checks it before the password, so a disabled user gets `NotAllowed`, shown as the generic error, and the failed-attempt count doesn't change. It covers every other sign-in path too.
  - Authorize and refresh in `ConnectEndpoints`: refuse when `DisabledAt` is set, next to the lockout check. The stamp change already ends sessions; the explicit check makes the reason independent of the stamp.
- **Rationale**: `CanSignInAsync` is the framework's hook for "this account may not sign in", and `PasswordSignInAsync` then reports `NotAllowed`, which the page shows as the generic error.

## R7. Events and contracts

- **Decision**:
  - `Identity.Contracts` (already packable with MinVer prefix `identity-contracts/v`): `TenantCreated(string TenantId, Guid AdminUserId)`, `UserRegistered(Guid UserId, string TenantId, string UserType, string Email)`, `UserDisabled(Guid UserId, string TenantId)` in namespace `Identity.Contracts`, with no package references. The message identity is the full type name (what `PublishEvent<T>` uses), so the namespace and record names are the contract and must not change within v0.
  - `Program.cs`: `PublishEvent<TenantCreated>()`, `PublishEvent<UserRegistered>()`, `PublishEvent<UserDisabled>()`, and `UseBuildingBlocksPermissionInvalidation()`.
  - Handlers return events as `OutgoingMessages`. Tenant creation sets the envelope tenant to the new tenant (`DeliveryOptions.TenantId`), because the platform caller has none.
  - Release: tag `identity-contracts/v0.1.0` after the owner confirms; `pack.ps1` already picks up packable projects.
- **Alternatives considered**: putting the records in Identity.Api (other services would need a project reference, forbidden by principle I).

## R8. Tenant creation shared with the demo seed

- **Decision**: `TenantCreation` domain service: validates the slug and name rules, creates `Tenant`, the admin `AppUser` through `UserManager`, the Administrator `RoleAssignment`, and returns the events. The handler sets the scoped `TenantContext` to the new tenant id before adding tenant-owned rows (the platform request has no tenant, and the save interceptor requires one). `DemoTenantSeeder` runs in `StartupTasks` after the platform admin: when `Identity:DemoTenant:*` is set and no tenant has the slug, it uses `TenantCreation` in a fresh scope with `IDbContextOutbox<IdentityDbContext>` and `SaveChangesAndFlushMessagesAsync`, so its events go through the outbox too.
- **Rationale**: one code path for the rules; the seed publishes events like a real creation (spec assumption).

## R9. Routes

- **Decision**: `MapPostToWolverine` / `MapGetToWolverine`-style endpoints in each feature folder. Platform routes `.RequirePlatform()`. Routes in [contracts/endpoints.md](contracts/endpoints.md). Role routes are idempotent `PUT` and `DELETE` on `/users/{id}/roles/{role}`.
- **Route test**: every new route is discovered by `NoRolesRouteCheck`. Route parameters get random GUIDs; command bodies are `{}`, which the R3 order turns into 403.

## R10. Moving the 011 roles slice

- **Decision**: `Roles/` moves to `Features/ListRoles/` with no behaviour change. `GET /roles` stays a tenant route and lists the tenant roles only; the platform role is not listed.
