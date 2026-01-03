Manual test plan (breakpoint-driven) for the core flows you listed, plus the other auth workflows that are critical in this codebase.

> This version is endpoint-driven and based on the actual controller routes in this repo:
> - `AuthController` -> `/api/auth/*`
> - `TenantController` -> `/api/tenants/*`
> - `InvitationController` -> `/api/invitations/*`
> - `TenantJoinLinkController` -> `/api/tenant-join-links/*` (CRUD, authenticated)
> - `JoinLinkController` -> `/api/join/*` (public redirect into Keycloak)

---

## Global setup / pre-checks (do once)
1. Start API in Debug with a clean DB (or known baseline).
2. Ensure Keycloak is running and reachable (admin + public endpoints).
3. Confirm `.env` / `appsettings.json` values:
   - Keycloak base URL (`Keycloak:AuthServerUrl`), default realm, client id/secret, redirect URIs.
   - JWT secret/issuer/audience.
4. Pick a single browser profile and clear cookies between scenarios (cookie state can mask bugs).
5. Put breakpoints in:
   - `AuthController.AuthCallback` -> `GET /api/auth/callback`
   - `AuthFlowService.HandleAuthCallbackAsync`
   - `AuthFlowService.HandleNewOrganizationFlowAsync`
   - `AuthFlowService.HandleInvitationFlowAsync`
   - `AuthFlowService.HandleEnterpriseFirstAdminFlowAsync`
   - `AuthFlowService.ValidateAndAssignSsoUserAsync`
   - `TenantController.ResolveRealm` -> `POST /api/tenants/resolve-realm`
   - `TenantController.ConfigureSsoSettings` -> `POST /api/tenants/{id}/sso-settings`
   - `TenantSsoSettingsService.ConfigureSsoSettingsAsync`
   - `InvitationController.Create` -> `POST /api/invitations`
   - `InvitationController.InviteRedirect` -> `GET /api/invitations/invite/{invitationToken}`
   - `TenantInvitationRepository.AcceptInvitationAsync`
   - `UserTenantRepository.AssignUserToTenantAsync`
   - `UserRoleRepository.AssignRoleAsync`
   - `JoinLinkController.JoinRedirect` -> `GET /api/join/{joinToken}`
   - `UserRepository.EnsureLocalUserExistsAsync`

During each scenario, verify DB state after each milestone:
- `Users` row creation (1 *global* user per person)
- `Tenants` creation
- `UserTenants` row creation (membership + `ExternalUserId`)
- `UserRoles` assignment (if applicable)
- `TenantInvitations` status transitions

---

## How these flows work in this API (quick mental model)
- You do **not** directly call “create user” endpoints.
- You call an endpoint that returns a Keycloak URL (or a public redirect endpoint that itself redirects to Keycloak).
- Keycloak redirects back to `GET /api/auth/callback?code=...&state=...`
- `AuthController.AuthCallback` calls `AuthFlowService.HandleAuthCallbackAsync`, which parses `state` and runs the appropriate flow:
  - `Flow = "new_org"`
  - `Flow = "invitation"` + `InvitationToken`
  - `Flow = "join_link"` + `JoinToken`
  - `Flow = "enterprise_first_admin"`
- On success, controller sets `AuthToken` cookie.

---

## Scenario 1: Standard tenant creation + first user signup
Goal: “new org” self-service flow creates a standard tenant and an initial membership/admin.

### Steps (Swagger + Browser)
1. **Generate a registration URL** (standard/shared realm):
   - `GET /api/auth/register?returnUrl=%2Fapp`
   - Example: `GET https://localhost:<port>/api/auth/register?returnUrl=%2Fapp`

2. Copy `data.authUrl` from the response and open it in a browser.

3. Complete Keycloak registration. Keycloak will redirect back to:
   - `GET /api/auth/callback?code=...&state=...`

4. In the callback response, confirm:
   - `success=true`
   - `token` present (and cookie should be set by controller)

5. Verify you’re authenticated:
   - `GET /api/auth/me`

### Things to verify step-by-step (breakpoints)
- `state` parsing: realm chosen correctly (default is `groundup` if missing).
- `keycloakUserId` extracted from `sub`.
- membership lookup: `_userTenantRepository.GetByRealmAndExternalUserIdAsync(realm, sub)`.
- user creation: `UserRepository.EnsureLocalUserExistsAsync` called exactly once.
- tenant creation: `ITenantRepository.CreateStandardTenantForUserAsync`.
- membership: `UserTenantRepository.AssignUserToTenantAsync(... isAdmin:true, externalUserId:sub)`.
- token issuance: `TokenService.GenerateTokenAsync` includes correct tenant context.
- cookie set **only** in `AuthController.AuthCallback`.

### DB assertions
- Exactly 1 standard tenant
- Exactly 1 user
- Exactly 1 `UserTenant` row linking the user to the tenant with correct `ExternalUserId` (Keycloak `sub`)

> Note: the *exact* `Flow="new_org"` value comes from `state`. If you don’t see `Flow="new_org"` during this scenario, your `AuthUrlBuilderService.BuildRegistrationUrlAsync` may be generating plain registration rather than the org-creation flow.

---

## Scenario 2: First user invitation to that standard tenant
Goal: invited user can join via invitation token; membership and (optional) roles assigned; invitation status updates.

### Steps
#### A) As tenant admin (already logged in)
1. **Create invitation**:
   - `POST /api/invitations`
   - Body (use Swagger schema for the exact DTO; typical example):
     - `email`: `"invitee.standard.1@testmail.com"`
     - `expirationDays`: `7`

2. Capture the invitation token from the response (commonly `InvitationToken` / `token`).

3. (Optional) Preview invitation details (public):
   - `GET /api/invitations/token/{token}`

#### B) As invited user (not logged in yet)
4. **Use the public invite link endpoint** (this returns a Keycloak URL with the correct `state`):
   - `GET /api/invitations/invite/{invitationToken}`

5. Open the returned `data.authUrl` in a browser.
   - New user: register
   - Existing user: login

6. Keycloak redirects back to:
   - `GET /api/auth/callback?code=...&state=...`

7. Verify invited user session:
   - `GET /api/auth/me`

### Things to verify
- Callback routes to invitation flow: `Flow="invitation"` + token present.
- Invitation lookup is valid/pending/not expired.
- Acceptance updates invitation status + sets `AcceptedByUserId`.
- Membership created with correct tenant + correct `ExternalUserId`.

### DB assertions
- `TenantInvitations` status transitioned to accepted
- New `UserTenants` row created
- User exists only once globally (no duplicates)

---

## Scenario 3: Enterprise tenant creation + first user signup
Goal: enterprise provisioning creates tenant + realm + first admin setup flow works, prevents re-running.

### A) Provision enterprise tenant (onboarding)
1. Call:
   - `POST /api/auth/enterprise/signup`

2. Body: use Swagger schema for `EnterpriseSignupRequestDto`.
   - Example shape (field names must match your DTO in Swagger):
     - `tenantName`: `"Acme Corp"`
     - `adminEmail`: `"first.admin@acme-test.com"`
     - `companyDomain`: `"acme-test.com"`

3. Confirm response returns a **first-admin Keycloak registration URL**.

4. Verify in Keycloak:
   - new realm created
   - client exists/configured as expected

### B) First admin registers (enterprise_first_admin flow)
5. Open the first-admin registration URL in a browser and complete registration.

6. Keycloak redirects back to:
   - `GET /api/auth/callback?code=...&state=...`

### Things to verify
- realm used is the enterprise realm, not `groundup`.
- “first admin only” guard works:
  - first attempt succeeds
  - second attempt fails with a proper error
- membership created as admin
- realm registration disabled at end (non-fatal if disabling fails)

### DB assertions
- tenant has `TenantType = Enterprise`, proper `RealmName` / domain settings
- exactly 1 initial `UserTenant` row for the first admin

---

## Scenario 4: Invitation into enterprise tenant
Goal: invitation flow uses the enterprise realm and stores realm-scoped `ExternalUserId`.

### Steps
1. As enterprise tenant admin, create invitation:
   - `POST /api/invitations`
   - Body typical example:
     - `email`: `"invitee.enterprise.1@acme-test.com"`
     - `expirationDays`: `7`

2. As invited user, call:
   - `GET /api/invitations/invite/{invitationToken}`

3. Open returned `data.authUrl` and complete Keycloak login/registration.

4. Keycloak redirects back to:
   - `GET /api/auth/callback?...`

### Things to verify
- `InvitationController.InviteRedirect` chooses realm from invitation `RealmName`.
- `ExternalUserId` stored on `UserTenant` matches Keycloak `sub` from the enterprise realm.

---

## Other critical workflows

### 1) Enterprise SSO auto-join (domain allowlist)
Goal: configuring allowlist lets matching domain users auto-join.

1. Configure SSO settings:
   - `POST /api/tenants/{id}/sso-settings`
   - Body: use Swagger schema for `ConfigureSsoSettingsDto`.

2. Login with a Keycloak user whose email domain is allowed.
   - Generate login URL:
     - `GET /api/auth/login?domain=acme-test.com&returnUrl=%2Fapp`
   - Open returned `data.authUrl` in browser.
   - Complete login -> callback hits `GET /api/auth/callback`.

3. Verify:
   - `ValidateAndAssignSsoUserAsync` ran
   - membership created
   - optional default role assigned (if your settings specify it)

### 2) Enterprise SSO blocked user (domain not allowed, no invitation)
1. Attempt login for a user with an email domain not in allowlist:
   - `GET /api/auth/login?domain=notallowed-test.com&returnUrl=%2Fapp`

2. Complete login and observe callback response.

3. Verify:
   - access is denied (expected failure path)
   - **no** membership created

### 3) Join link flow
Goal: join link redirects through Keycloak and results in membership creation via callback `Flow="join_link"`.

#### A) Create join link (authenticated)
1. Create join link:
   - `POST /api/tenant-join-links`
   - Body: use Swagger schema for `CreateTenantJoinLinkDto`.

2. Response will include `joinToken` and `joinUrl` (added server-side):
   - `joinUrl` looks like: `https://localhost:<port>/api/join/{joinToken}`

#### B) Use join link (public)
3. As a not-logged-in user, open:
   - `GET /api/join/{joinToken}`
   - This endpoint redirects to Keycloak and embeds `Flow="join_link"` in `state`.

4. Complete login/registration in Keycloak; it redirects back to:
   - `GET /api/auth/callback?...`

5. Verify:
   - revoked/expired enforced
   - membership created
   - “already a member” handled correctly

### 4) Multi-tenant user requiring tenant selection
Goal: ensure `POST /api/auth/set-tenant` lists tenants and issues updated token.

1. Login normally and ensure a single user has memberships in multiple tenants.

2. Call:
   - `POST /api/auth/set-tenant`

3. Body (to request selection):
```json
{ "tenantId": null }
```

4. If response says selection required, pick a tenant id and call again:
```json
{ "tenantId": 123 }
```

5. Verify:
   - token regenerated
   - cookie updated in controller
   - tenant claims/tenant context correct

### 5) Realm resolution (domain login)
Goal: confirm correct realm returned for default and enterprise custom domains.

1. Call:
   - `POST /api/tenants/resolve-realm`

2. Body:
```json
{ "url": "https://app.acme-test.com" }
```

3. Verify `data.realm` and `data.isEnterprise` in response.

---

## What to watch for during cleanup as you debug
As you step through, flag these “smells” for follow-up cleanup:
- Any remaining code paths that imply `User.Id` comes from Keycloak (e.g., `Guid.Parse(keycloakUser.Id)` logic).
- Any service writing to `_dbContext.*` directly.
- Any repository methods that are permission-gated but used by unauthenticated flows.
- Any duplicated logic across invitation/join-link/default flows (especially membership creation + role assignment).

If you want, after your first manual walkthrough, share the first 1–2 confusing call stacks or “why is this here” sections and I can propose targeted deletions/consolidations without broad rewrites.
