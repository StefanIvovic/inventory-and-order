---

description: "Task list for feature 014 Identity Customers"
---

# Tasks: Identity Customers

**Input**: Design documents from `specs/014-identity-customers/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Write each test first and see it fail.

**Global rules**: no comments (C#, cshtml); no `SaveChanges` in handlers; sign-in never reveals other accounts; Testcontainers only; build green after every task.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Foundational

- [ ] T001 `Users/AppUser.cs`: customers get user name `<tenantId>:<email>` (data-model.md `AppUser.UserName`); `Program.cs`: `RequireUniqueEmail = false`, `AllowedUserNameCharacters = null` (research.md R2). Switch tests that use `FindByEmailAsync` to `FindByNameAsync`. Existing suite green, including the 013 duplicate-email tests, which now detect duplicates through the user name
- [ ] T002 `Persistence/IdentityDbContext.cs`: `ux_identity_users_customer_email` (data-model.md `AppUser.NormalizedEmail`); migration `Customers`, which also widens `UserName` and `NormalizedUserName` to 320 (data-model.md `AppUser.UserName`); `UniqueViolationMapper` maps it to `email-taken` (research.md R4)
- [ ] T003 [P] `Protocol/ClientPolicies.cs` per data-model.md client policies, and `Protocol/SignInTarget.cs` that reads client and tenant from a local authorization return URL (research.md R1, R2), with unit tests in `Identity.Tests/Protocol/SignInTargetTests.cs`
- [ ] T004 Test support: `AuthorizeRequest` gains `Tenant`; `IdentityApp` gains storefront client constants and a `RegisterAsync` page helper in `Identity.Tests/Flow/CodeFlow.cs`

---

## Phase 2: User Story 3 - Each client accepts only its kind of user (P1)

- [ ] T005 [US3] Tests first in `Identity.Tests/Flow/ClientPolicyTests.cs`: storefront without tenant and with unknown tenant (error to the client, no login page); staff on storefront and customer on backoffice (generic error, no code); customer of another tenant (generic error); a mismatching login session is asked to sign in again; the tenant parameter is ignored on backoffice; a code redeemed or refreshed by the wrong client is refused. See them fail
- [ ] T006 [US3] `Connect/ConnectEndpoints.cs`: tenant parameter, client policy at authorize, session match, policy re-check at the token endpoint (contracts/protocol.md)

---

## Phase 3: User Story 2 - Customers sign in per tenant (P1)

- [ ] T007 [US2] Tests first in `Identity.Tests/Flow/CustomerSignInTests.cs`: the same email as customers of two tenants and as staff signs in to exactly the requested account; lockout applies to customers. See them fail
- [ ] T008 [US2] `Pages/Account/Login.cshtml(.cs)`: `SignInTarget` decides the user name; storefront heading shows the tenant name and a "Create an account" link (contracts/protocol.md)

---

## Phase 4: User Story 1 - Registration (P1)

- [ ] T009 [US1] Tests first in `Identity.Tests/Flow/RegistrationTests.cs`: the full storefront flow (authorize → login → register → code → token with `user_type=customer` and the tenant, one `UserRegistered`); field errors (data-model.md `RegisterCustomer.Email`, `RegisterCustomer.Password`, `RegisterCustomer.ConfirmPassword`); duplicate in the same tenant ("already exists", nothing created); same email in another tenant and as staff succeeds; concurrent duplicates create one account; unknown and missing tenant show no form; direct registration lands on the signed-in page; antiforgery required. See them fail
- [ ] T010 [US1] `Features/RegisterCustomer/`: command, `CommandValidator`, handler with `HandlerUserManager` and `UserRegistered` with the envelope tenant (research.md R3, data-model.md `RegisterCustomer.TenantId`)
- [ ] T011 [US1] `Pages/Account/Register.cshtml(.cs)`: form, tenant name, errors, "already exists" with sign-in link, sign-in and local redirect (contracts/protocol.md)

---

## Phase 5: User Story 4 - Customers stay out of staff management (P2)

- [ ] T012 [US4] Tests first in `Identity.Tests/Users/CustomerSeparationTests.cs`: the user list shows staff only; disable and role routes return 404 for a customer id; a customer token gets 403 on every Identity route; `OwnTokenTests` asserts `/account/register` is allow-listed. See them fail
- [ ] T013 [US4] `Users/TenantUsers.cs` and `Features/ListUsers/ListUsersHandler.cs` filter staff; `IdentityPermissionProvider` returns none for customers (research.md R5)

---

## Phase 6: Polish

- [ ] T014 `codegen.ps1 -Service identity`; run the full Identity suite twice, the cleanup check, the comment scan; quickstart.md section 2 on the dev stack, then reset
- [ ] T015 Commit. After the owner confirms, push and open the PR, and confirm `identity.yml` and `deploy.yml` pass

---

## Dependencies

- Phase 1 → everything. T003 ∥ T001–T002
- US3 before US2 (the login page relies on the authorize checks). US1 needs US2 (the register link and sign-in). US4 is independent of US1–US3
- Polish last

## Implementation Strategy

- MVP: Phases 1–4, the full storefront flow with registration.
- One PR, no new package or contract versions.
