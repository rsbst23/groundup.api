# Next Thread: Finish GroundUp Auth & Onboarding Work

Use this document to pick up where the current work left off and finish the remaining tasks. Keep changes small, run the build often, and validate each step with tests.

## Current repository state (summary)
- Build: successful after Phase 1 entity and repository refactors.
- Key structural decisions already applied:
  - `Tenant.Id` remains `int`.
  - `UserTenant` now contains `ExternalUserId` (Keycloak `sub`) and `IsAdmin` flag.
  - `Tenant` uses typed enums `TenantType` and `OnboardingMode`, with `RealmName` and `CustomDomain`.
  - `TenantInvitation` uses `Status: InvitationStatus` and `RoleId` + `IsAdmin` (legacy string `AssignedRole` removed except where intentionally kept).
  - `TenantJoinLink` entity added.
  - `UserKeycloakIdentity` is still present (kept for compatibility) but can be removed later.

## Top priorities (must do in the next thread)
1. Auth callback & membership wiring
   - Update `/api/auth/callback` (AuthController) to resolve membership using:
     - realmName (from issuer)
     - `sub` claim (Keycloak user id)
     - Query: `UserTenant` WHERE Tenant.RealmName = @realmName AND ExternalUserId = @sub
   - If no UserTenant found, run bootstrap flows: create User (by email), create Tenant (standard signup) or bind to existing Tenant (enterprise invite/first-admin flow).
   - Ensure `UserTenant.ExternalUserId` is created when accepting invitations / join links or during enterprise bootstrap.

2. Invitation & Join-link endpoints
   - Implement/finish public endpoints referenced by spec:
     - `GET /invite/{invitationToken}` ? validate invitation, set state, redirect to Keycloak (shared realm)
     - `GET /enterprise/invite/{invitationToken}` ? SSO enterprise invite redirect
     - `GET /join/{joinToken}` ? validate join link, set state, redirect to Keycloak (shared realm)
   - Ensure `AuthController.Callback` decodes OIDC `state` and performs correct accept/assign behavior (create `UserTenant`, set `UserRole` as per `RoleId`, mark invitation `Accepted`).

3. Enterprise signup flow
   - `POST /api/tenants/enterprise/signup` (TenantController already updated) — finish wiring to:
     - Create realm via `IdentityProviderAdminService` (already called)
     - Create `Tenant` (done)
     - Create first admin invitation and send email (email service placeholder exists)
   - Add endpoint(s) used by Keycloak required-actions redirect (e.g., `/enterprise/welcome`), which should start OIDC login for that realm and result in callback handling that binds identity to tenant.

4. Remove or migrate away from `UserKeycloakIdentity` usage
   - Decide to either remove the table and repository or keep it for account-linking features.
   - If removing, update all code that calls `IUserKeycloakIdentityRepository` to use `UserTenant` queries instead.

## Secondary tasks (next after top priorities)
- Add `TenantJoinLink` APIs (create/list/revoke).
- Implement invitation email sending (SMTP/SES) and use `execute-actions-email` for enterprise local invite flows where appropriate.
- Update mapping, DTOs and frontend contract notes as necessary.
- Add integration tests for invite & join flows (end-to-end using Keycloak test instance or mocks).

## Database & migrations
- Because this is a dev-only workspace and you said DB can be reset, recommended approach:
  1. Drop and recreate the dev database (clean state).
  2. Apply EF migrations (or generate a new consolidated migration) and run `dotnet ef database update`.
- Alternatively, if you prefer migrations: produce and commit EF migrations for the entity changes and apply them.

## Files to inspect / edit next (high value)
- `GroundUp.api/Controllers/AuthController.cs` (callback logic)
- `GroundUp.api/Controllers/TenantController.cs` (enterprise signup already updated)
- `GroundUp.api/Controllers/InvitationController.cs` or `TenantInvitationController.cs` (invite endpoints)
- `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs` (acceptance logic was updated — review)
- `GroundUp.infrastructure/repositories/UserTenantRepository.cs` (assign logic)
- `GroundUp.infrastructure/services/IdentityProviderAdminService.cs` (Keycloak admin API integration)
- `GroundUp.infrastructure/mappings/MappingProfile.cs` (mappers)

## Tests & verification
- Run:
  - `dotnet build` (root solution)
  - `dotnet test` for unit and integration projects
- Manual flows to validate with Keycloak dev instance:
  - Standard signup ? creates Tenant + UserTenant + UserRole
  - Enterprise signup ? creates realm, Tenant, first-invitation email link, admin accepts and is bound
  - Invite flow ? invite email ? redirect ? callback binds identity
  - Join link ? link redirect ? user bound and role assigned

## Assumptions & decisions (recorded)
- Tenant.Id stays as `int`.
- Use `UserTenant.ExternalUserId` as the canonical external identity per-tenant.
- No default roles seeded by GroundUp.
- Database may be reset in dev; migrations can assume a clean DB.

## Handover notes for the next thread
- Start with the Auth callback changes and verify membership resolution works using `UserTenant.ExternalUserId`.
- Implement invite/join endpoints that create the correct OIDC `state` JSON and sign/encrypt it if required by your existing `state` implementation.
- Keep changes minimal and run builds/tests after each change.

If you want, I can now generate EF migration(s) for the current model, or implement the AuthController callback changes next. Specify which and I will proceed.
