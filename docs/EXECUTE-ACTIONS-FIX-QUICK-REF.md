# Execute-Actions Fix: Quick Reference

## The Problem

```
User sets password ? "Account updated" ? STUCK (no way back)
```

## Root Cause

Missing `client_id` parameter in execute-actions-email API call.

Keycloak requires **BOTH** `client_id` AND `redirect_uri` together.

## The Fix

### API Call

**Before (Broken):**
```
PUT /execute-actions-email?redirect_uri=http://...
```

**After (Fixed):**
```
PUT /execute-actions-email?client_id=groundup-api&redirect_uri=http://...
```

### Code Change

```csharp
await _identityProvider.SendExecuteActionsEmailAsync(
    realm: "tenant_xyz",
    userId: userId,
    actions: new List<string> { "UPDATE_PASSWORD", "VERIFY_EMAIL" },
    clientId: "groundup-api",           // ? ADD THIS
    redirectUri: "http://localhost:5123/complete"
);
```

## Result

### Before Fix
```
"Your account has been updated"
[No link back to application]
```

### After Fix
```
"Your account has been updated"
[Back to application] ? Redirects to your app
```

## Quick Test

```bash
# 1. Create user
POST /admin/realms/{realm}/users

# 2. Send execute-actions with BOTH parameters
PUT /admin/realms/{realm}/users/{userId}/execute-actions-email?client_id=groundup-api&redirect_uri=http://localhost:5123/complete

# 3. Check email, complete actions
# 4. Should see "Back to application" link
# 5. Click link ? Should redirect to http://localhost:5123/complete
```

## Important Notes

1. **Both parameters required** - `client_id` AND `redirect_uri` together
2. **Not auto-redirect** - Shows success page with manual link
3. **redirect_uri must be whitelisted** - In client's Valid Redirect URIs
4. **Client must exist** - In the specified realm

## Why We're Not Using It

We chose **registration-based invitations** instead:

```
User clicks invitation ? Registers ? Auto-login ? Access granted
```

**Benefits:**
- ? One email (not two)
- ? Seamless flow
- ? No manual clicking
- ? Better UX

## When to Use This Fix

- Existing user invitations
- Password reset flows
- Email verification
- Any execute-actions scenario

## redirect_uri Rules

### ? Don't use Keycloak URL
```
http://localhost:8080/realms/{realm}/protocol/openid-connect/auth
```

### ? Use your application URL
```
http://localhost:5123/api/invitations/invite/{token}
```

## Client Configuration

Ensure in Keycloak:

```
Client: groundup-api
Valid Redirect URIs:
  - http://localhost:5123/*
  - http://localhost:5173/*
Standard Flow: Enabled
```

## Files Changed

- `IIdentityProviderAdminService.cs` - Added `clientId` parameter
- `IdentityProviderAdminService.cs` - Implemented fix

## Status

? **Fixed** - client_id parameter added  
? **Build successful** - No breaking changes  
?? **Not currently used** - Using registration flow instead  
?? **Ready when needed** - Available for other use cases  

---

**TL;DR:** Add `client_id` parameter to execute-actions-email calls. Keycloak needs both `client_id` and `redirect_uri` together to show "Back to application" link.
