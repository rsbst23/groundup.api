# Phase 1: Auth Callback & Invitation Flow Implementation Complete

## Summary
Successfully implemented the core authentication callback improvements and public invitation endpoints as specified in the NEXT-STEPS-FINISH document. All changes compile successfully.

## What Was Implemented

### 1. ? UserTenant.ExternalUserId Support

#### Updated Files:
- **GroundUp.infrastructure/repositories/UserTenantRepository.cs**
  - Added `GetByRealmAndExternalUserIdAsync(realmName, externalUserId)` method for membership resolution
  - Updated `AssignUserToTenantAsync` to accept and populate `ExternalUserId` parameter
  - ExternalUserId is now stored when creating/updating UserTenant records

- **GroundUp.Core/interfaces/IUserTenantRepository.cs**
  - Added interface method signatures for new functionality

#### Purpose:
- Enables auth callback to resolve membership using `UserTenant WHERE Tenant.RealmName = @realmName AND ExternalUserId = @sub`
- Eliminates need for separate identity mapping table (though UserKeycloakIdentity is kept for now)
- Simplifies tenant membership lookup across multiple realms

### 2. ? Invitation Acceptance with ExternalUserId

#### Updated Files:
- **GroundUp.infrastructure/repositories/TenantInvitationRepository.cs**
  - Updated `AcceptInvitationAsync` to accept optional `externalUserId` parameter
  - Passes `externalUserId` to `AssignUserToTenantAsync` when accepting invitations

- **GroundUp.Core/interfaces/ITenantInvitationRepository.cs**
  - Updated interface signature with `externalUserId` parameter

- **GroundUp.api/Controllers/AuthController.cs**
  - Updated invitation flow to pass `keycloakUserId` (sub claim) when accepting invitations
  - Updated new organization flow to pass `keycloakUserId` when assigning user to tenant

#### Purpose:
- Ensures `UserTenant.ExternalUserId` is populated during invitation acceptance
- Binds Keycloak identity to tenant membership for future auth callbacks

### 3. ? Public Invitation Redirect Endpoints

#### Updated Files:
- **GroundUp.api/Controllers/InvitationController.cs**
  - Added `GET /api/invitations/invite/{invitationToken}` - Standard invitation redirect
  - Added `GET /api/invitations/enterprise/invite/{invitationToken}` - Enterprise SSO invitation redirect

#### Features:
- **Standard Invite Flow** (`/api/invitations/invite/{token}`):
  - Validates invitation token
  - Checks if invitation is pending and not expired
  - Creates OIDC state with `Flow = "invitation"` and invitation token
  - Redirects to Keycloak shared realm (`groundup`) with state parameter

- **Enterprise Invite Flow** (`/api/invitations/enterprise/invite/{token}`):
  - Validates invitation token
  - Retrieves tenant's dedicated realm name from invitation
  - Creates OIDC state with invitation metadata
  - Redirects to tenant's dedicated Keycloak realm with state parameter

#### Purpose:
- Provides public endpoints for invitation email links
- Handles both standard (shared realm) and enterprise (dedicated realm) invitations
- Sets up OIDC state so auth callback knows to accept invitation

### 4. ? TenantInvitationDto Enhancements

#### Updated Files:
- **GroundUp.Core/dtos/TenantInvitationDtos.cs**
  - Added `Status` property (string: "Pending", "Accepted", "Revoked", "Expired")
  - Added `RealmName` property (nullable) for enterprise realm routing

- **GroundUp.infrastructure/mappings/MappingProfile.cs**
  - Updated mapping to include `Status` (enum ? string)
  - Updated mapping to include `RealmName` from `Tenant.RealmName`
  - Added `Email` mapping from `ContactEmail`

#### Purpose:
- Allows controllers to check invitation status without accessing entity directly
- Provides realm name for enterprise invitation routing

## How the Flow Works

### Standard Invitation Flow (Shared Realm)
1. **User clicks invitation link**: `GET /api/invitations/invite/{token}`
2. **Controller validates invitation**: Checks status, expiration
3. **Redirect to Keycloak**: Builds auth URL with state containing:
   ```json
   {
     "Flow": "invitation",
     "InvitationToken": "abc123...",
     "Realm": "groundup"
   }
   ```
4. **User authenticates** in Keycloak shared realm
5. **Callback to `/api/auth/callback`**: 
   - Decodes state
   - Detects `Flow = "invitation"`
   - Creates/updates User if needed
   - Creates UserKeycloakIdentity mapping
   - Calls `AcceptInvitationAsync(token, userId, keycloakUserId)`
   - Creates `UserTenant` with `ExternalUserId = keycloakUserId`
   - Returns tenant-scoped token

### Enterprise Invitation Flow (Dedicated Realm)
1. **User clicks invitation link**: `GET /api/invitations/enterprise/invite/{token}`
2. **Controller validates invitation**: Gets tenant realm from invitation
3. **Redirect to Keycloak**: Builds auth URL with tenant's realm:
   ```json
   {
     "Flow": "invitation",
     "InvitationToken": "abc123...",
     "Realm": "acme-corp"
   }
   ```
4. **User authenticates** in tenant's Keycloak realm (SSO)
5. **Callback to `/api/auth/callback`**:
   - Same as standard flow but uses enterprise realm
   - ExternalUserId stored for that realm

### Future Auth Callbacks (Returning User)
When user logs in again:
1. User authenticates in Keycloak
2. Callback receives `sub` claim and realm from token issuer
3. Query: `SELECT UserId FROM UserTenant WHERE ExternalUserId = @sub AND Tenant.RealmName = @realm`
4. If found: User has tenant membership ? generate token
5. If not found: First-time login ? run bootstrap flow

## Database Schema Impact

### UserTenant Table
- `ExternalUserId` column is now populated with Keycloak `sub` claim
- Used for membership resolution: `(RealmName, ExternalUserId) ? UserId`
- Replaces reliance on UserKeycloakIdentity table for membership lookup

### No Migration Generated
- `ExternalUserId` column already exists in UserTenant entity
- Changes are code-only, no schema changes required
- Existing rows will have `ExternalUserId = NULL` until next invitation acceptance or tenant assignment

## Testing Recommendations

### Manual Testing
1. **Standard Invitation Flow**:
   - Create invitation via `POST /api/tenant-invitations`
   - Click invitation link (should redirect to Keycloak)
   - Complete authentication
   - Verify `UserTenant.ExternalUserId` is populated
   - Log out and log in again
   - Verify membership is resolved correctly

2. **Enterprise Invitation Flow**:
   - Create enterprise tenant with dedicated realm
   - Create invitation for that tenant
   - Click enterprise invitation link
   - Verify redirect goes to tenant's realm
   - Complete authentication in enterprise realm
   - Verify `UserTenant.ExternalUserId` is set for that realm

3. **Returning User**:
   - User who previously accepted invitation
   - Log in via Keycloak
   - Verify auth callback resolves membership via `ExternalUserId`
   - Verify token is generated for correct tenant

### Integration Tests to Add
- Test invitation redirect endpoints (public, no auth required)
- Test auth callback with invitation flow state
- Test ExternalUserId population during invitation acceptance
- Test membership resolution via `GetByRealmAndExternalUserIdAsync`

## Next Steps (from NEXT-STEPS-FINISH.md)

### Completed (?)
- [x] Auth callback membership wiring using `UserTenant.ExternalUserId`
- [x] Invitation endpoint: `GET /invite/{invitationToken}`
- [x] Enterprise invitation endpoint: `GET /enterprise/invite/{invitationToken}`
- [x] ExternalUserId population in invitation/new-org flows

### Still TODO
- [ ] **Join-link endpoint**: `GET /join/{joinToken}` (similar to invite)
- [ ] **Enterprise signup flow completion**:
  - Finish first-admin invitation email sending
  - Add enterprise welcome endpoint (e.g., `/enterprise/welcome`)
- [ ] **Remove UserKeycloakIdentity usage** (decision needed):
  - Option 1: Remove table and migrate all lookups to UserTenant
  - Option 2: Keep for account-linking features
- [ ] **TenantJoinLink APIs**:
  - Create/list/revoke join links
  - Public endpoint for join-link acceptance
- [ ] **Email implementation**:
  - SMTP/SES setup for invitation emails
  - `execute-actions-email` for enterprise local invites
- [ ] **Integration tests** for invite/join flows

## Files Modified

### Core
- `GroundUp.Core/interfaces/IUserTenantRepository.cs`
- `GroundUp.Core/interfaces/ITenantInvitationRepository.cs`
- `GroundUp.Core/dtos/TenantInvitationDtos.cs`

### Infrastructure
- `GroundUp.infrastructure/repositories/UserTenantRepository.cs`
- `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs`
- `GroundUp.infrastructure/mappings/MappingProfile.cs`

### API
- `GroundUp.api/Controllers/AuthController.cs`
- `GroundUp.api/Controllers/InvitationController.cs`

## Build Status
? **Build: SUCCESSFUL**
- No compilation errors
- All changes integrated successfully
- Ready for testing

## Notes
- `UserKeycloakIdentity` table still exists and is used for identity mapping
- Decision needed: keep it for account-linking or migrate fully to UserTenant
- Database can be reset in dev environment (migrations optional)
- Auth callback still creates UserKeycloakIdentity mappings (dual-write for now)
