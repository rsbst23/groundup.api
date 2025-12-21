# Invitation URL Bug - Current State & Fix Needed

## Problem Summary

When creating an invitation via `POST /api/invitations`, the generated URL sends users to Keycloak's **login** page instead of the **registration** page. This causes invited users (who don't have accounts yet) to see "Invalid username or password" errors.

## Current Broken URL

```
http://localhost:8080/realms/tenant_rob01_8765/protocol/openid-connect/auth?...
```

This is a **LOGIN** endpoint - requires existing account ?

## Should Be

```
http://localhost:8080/realms/tenant_rob01_8765/protocol/openid-connect/registrations?...
```

This is a **REGISTRATION** endpoint - creates new account ?

## Root Cause

The invitation URL generation is using `/auth` instead of `/registrations`. This needs to be fixed in the repository or controller that creates invitations.

## Where to Look

The bug is likely in one of these files:

1. **GroundUp.infrastructure/repositories/TenantInvitationRepository.cs** - `AddAsync` method
2. **GroundUp.api/Controllers/InvitationController.cs** - invitation creation endpoint
3. **A helper method** that builds Keycloak URLs for invitations

## What Needs to Change

Search for code that builds invitation URLs like this:

```csharp
// WRONG - This is login
$"{keycloakAuthUrl}/realms/{realmName}/protocol/openid-connect/auth"
```

Change to:

```csharp
// CORRECT - This is registration
$"{keycloakAuthUrl}/realms/{realmName}/protocol/openid-connect/registrations"
```

## Expected Flow After Fix

1. Admin creates invitation ? `POST /api/invitations`
2. System generates invitation with **registration** URL
3. User clicks invitation link ? Goes to Keycloak **registration** form
4. User fills out form (email, name, password) ? Creates Keycloak account
5. Keycloak redirects to `/api/auth/callback` with invitation token in state
6. API processes invitation flow ? Creates GroundUp user + assigns to tenant
7. User is logged in ? Success! ?

## Current Database State

- **Enterprise Tenant Created**: Rob Corp (tenant ID 6)
  - Realm: `tenant_rob01_8765`
  - First admin registered successfully
  - Registration disabled (correct!)
  
- **Invitation Created**: 
  - Token: `227b3431abcf42aeb17398e76daf3d28`
  - Email: (invited user's email)
  - Status: Pending
  - **Problem**: URL uses `/auth` instead of `/registrations`

## State in Auth Callback

When invitation URL is clicked, the state parameter contains:

```json
{
  "Flow": "invitation",
  "InvitationToken": "227b3431abcf42aeb17398e76daf3d28",
  "JoinToken": null,
  "RedirectUrl": null,
  "Realm": "tenant_rob01_8765"
}
```

This is correct - the flow is set to "invitation" and token is present. The only problem is the URL endpoint itself.

## Key Insight

**Invitations are ALWAYS for NEW users**, so they should ALWAYS get **registration** URLs, never login URLs!

- ? First admin: `/registrations` (from enterprise signup)
- ? Invited users: `/registrations` (from invitations) ? **THIS IS BROKEN**
- ? Existing users: `/auth` (login)

## Testing After Fix

### 1. Create New Invitation

```sh
POST http://localhost:5123/api/invitations
Content-Type: application/json
Authorization: Bearer <admin-token>

{
  "email": "newuser@example.com",
  "isAdmin": false,
  "expirationDays": 7
}
```

### 2. Check Response URL

The `invitationUrl` field should contain:
```
/protocol/openid-connect/registrations  ?
```

NOT:
```
/protocol/openid-connect/auth  ?
```

### 3. Visit URL in Browser

Should see:
- ? Keycloak **registration form** with fields for new account
- ? NOT a login form asking for existing credentials

### 4. Complete Registration

- Fill out form ? Submit
- Should be redirected to API callback
- Should create user and assign to tenant
- Should be logged in successfully

## Files to Search

Use these search queries to find the bug:

1. Search for: `"InvitationUrl"` or `"invitationUrl"`
2. Search for: `"/protocol/openid-connect/auth"`
3. Look in:
   - `TenantInvitationRepository.cs`
   - `InvitationController.cs`
   - Any URL builder methods

## Additional Context

### Why This Matters

- Enterprise tenants have registration **disabled** after first admin
- Invited users CAN'T self-register
- They MUST use the invitation URL to register
- If the URL sends them to LOGIN instead of REGISTRATION, they're stuck!

### Related Working Code

The enterprise signup already does this correctly:

```csharp
// From TenantController.cs - EnterpriseSignup method
var registrationUrl = $"{keycloakAuthUrl}/realms/{realmName}/protocol/openid-connect/registrations" +
    $"?client_id={Uri.EscapeDataString(clientId)}" +
    $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
    $"&response_type=code" +
    $"&scope=openid%20email%20profile" +
    $"&state={Uri.EscapeDataString(stateEncoded)}";
```

The invitation URL generation should follow the same pattern!

## Next Steps

1. Find where `InvitationUrl` is set in the invitation creation code
2. Change `/auth` to `/registrations`
3. Test creating a new invitation
4. Verify URL contains `/registrations`
5. Test clicking the URL ? should see registration form
6. Test completing registration ? should work end-to-end

## Questions to Ask in New Thread

1. "Can you search for where invitations build the Keycloak URL with `/auth` and change it to `/registrations`?"
2. "Show me the `AddAsync` method in `TenantInvitationRepository.cs`"
3. "How is the `InvitationUrl` field populated when creating an invitation?"

---

**Status**: Bug identified, fix location needed  
**Priority**: High - Blocks enterprise tenant invitation flow  
**Impact**: Enterprise tenants cannot invite additional users  
**Created**: 2025-01-XX
