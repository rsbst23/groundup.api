# Invitation Redirect URI Fix

## Problem

When creating invitations for enterprise tenant users, the execute-actions email API call was failing with:

```json
{"errorMessage":"Invalid redirect uri."}
```

**Root Cause:** The redirect URI we were passing to Keycloak (`http://localhost:5123/api/invitations/invite/{token}`) was **not registered** as a valid redirect URI in the Keycloak client configuration.

## Investigation

When sending the execute-actions email, we call:
```
PUT /admin/realms/{realm}/users/{userId}/execute-actions-email?client_id=groundup-api&redirect_uri=http://localhost:5123/api/invitations/invite/{token}
```

However, the `groundup-api` client only had these redirect URIs configured:
- `https://rob05.com/*`
- `https://rob05.com/auth/callback`
- `http://localhost:5f23/api/auth/callback`

The `/api/invitations/invite/*` pattern was **missing**!

## Solution

### 1. Update Client Configuration (Automatic for New Realms)

Updated `IdentityProviderAdminService.BuildClientConfiguration()` to automatically include the invitation URL pattern when creating clients:

```csharp
// Add invitation acceptance URL (for execute-actions email redirect)
redirectUris.Add($"{apiUrl}/api/invitations/invite/*");
```

This ensures that **new enterprise realms** will have the invitation URL pattern in their client configuration.

### 2. Manual Fix for Existing Realms

For existing realms, you need to manually add the redirect URI pattern in Keycloak Admin Console:

1. Log into Keycloak Admin Console
2. Select your enterprise realm (e.g., `tenant_rob05_aabd`)
3. Go to **Clients** ? **groundup-api**
4. Go to **Settings** tab
5. Under **Valid redirect URIs**, click "+ Add valid redirect URIs"
6. Add: `http://localhost:5123/api/invitations/invite/*`
7. For production, also add: `https://your-api-domain.com/api/invitations/invite/*`
8. Click **Save**

## How It Works Now

### Invitation Flow

1. **Admin creates invitation** ? Creates Keycloak user
2. **System sends execute-actions email** with:
   - `client_id=groundup-api`
   - `redirect_uri=http://localhost:5123/api/invitations/invite/{token}`
3. **User clicks email link** ? Goes to Keycloak to set password and verify email
4. **User completes actions** ? Clicks "Back to application" button
5. **Keycloak redirects to** ? `http://localhost:5123/api/invitations/invite/{token}`
6. **Invitation endpoint** ? Validates invitation and returns login URL with OAuth state
7. **User authenticates** ? Standard OAuth login flow
8. **Auth callback** ? Processes invitation acceptance based on state

### Why This URL Pattern?

The `/api/invitations/invite/{token}` endpoint is a **PUBLIC endpoint** that:
- Validates the invitation is still pending and not expired
- Returns a Keycloak login URL with the invitation token in the OAuth `state` parameter
- Allows users to complete password setup before logging in

This is different from `/api/auth/callback` which handles the **OAuth callback after successful authentication**.

## Files Changed

### `GroundUp.infrastructure/services/IdentityProviderAdminService.cs`
- Added invitation URL pattern to `BuildClientConfiguration()`
- Ensures new realms automatically have correct redirect URIs

### `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs`
- Continued using invitation URL as redirect URI (as intended)
- Added detailed logging to debug email sending issues

## Testing

### For New Enterprise Tenants

1. Create a new enterprise tenant
2. Verify client has invitation URL in redirect URIs:
   ```
   GET /admin/realms/{realm}/clients
   ```
3. Create an invitation
4. User should receive email successfully

### For Existing Enterprise Tenants

After manually adding the redirect URI:

1. Create a new invitation
2. Check logs - should see:
   ```
   ? Successfully created Keycloak user {userId}
   ?? Attempting to send execute-actions email...
   Execute actions email response status: 204
   ? Successfully sent execute actions email
   ```
3. User should receive email with password setup link
4. After completing actions, user clicks "Back to application"
5. Should redirect to `/api/invitations/invite/{token}`
6. Should return login URL with invitation in state

## Configuration Requirements

### Environment Variables

```bash
API_URL=http://localhost:5123                    # Your API base URL
KEYCLOAK_RESOURCE=groundup-api                   # OAuth client ID
KEYCLOAK_AUTH_SERVER_URL=http://localhost:8080   # Keycloak base URL
```

### Keycloak Client Settings

**Valid Redirect URIs must include:**
- `/api/auth/callback` - OAuth login callback
- `/api/invitations/invite/*` - Execute-actions email redirect
- Frontend URLs (for frontend-initiated flows)

**Example for Development:**
```
http://localhost:5123/api/auth/callback
http://localhost:5123/api/invitations/invite/*
http://localhost:5173/auth/callback
http://localhost:5173/*
```

**Example for Production:**
```
https://api.yourapp.com/api/auth/callback
https://api.yourapp.com/api/invitations/invite/*
https://app.yourapp.com/auth/callback
https://app.yourapp.com/*
```

## Related Documentation

- [EXECUTE-ACTIONS-CLIENT-ID-FIX.md](./EXECUTE-ACTIONS-CLIENT-ID-FIX.md) - Previous fix for client_id parameter
- [INVITATION-EMAIL-FIX.md](./INVITATION-EMAIL-FIX.md) - Fix for required actions not triggering email
- [LOCAL-ACCOUNT-INVITATION-TESTING.md](./LOCAL-ACCOUNT-INVITATION-TESTING.md) - Testing guide

## Common Issues

### Email still not being sent?

1. **Check SMTP configuration** in the realm (not just master realm)
2. **Check Keycloak logs** for email sending errors
3. **Verify redirect URI** is in client configuration
4. **Check API logs** for execute-actions response status

### "Invalid redirect uri" error?

- The redirect URI is not in the client's Valid redirect URIs list
- Add the pattern to Keycloak client configuration (see Manual Fix above)

### User receives email but "Back to application" doesn't work?

- Redirect URI might be blocked by browser/firewall
- Check browser console for errors
- Verify API is accessible from user's browser
