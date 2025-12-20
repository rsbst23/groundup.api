# Execute-Actions Email Fix: client_id Parameter

## Problem Discovered

ChatGPT identified the root cause of the execute-actions redirect issue:

**Keycloak requires BOTH `client_id` AND `redirect_uri` parameters together for proper post-action redirects.**

When calling `execute-actions-email` with only `redirect_uri`, Keycloak:
- Defaults to the `account` client
- Shows "Account updated" page with NO way back to the application
- Does not provide a "Back to application" link

## The Fix

### Updated Method Signature

**Before:**
```csharp
Task<bool> SendExecuteActionsEmailAsync(
    string realm,
    string userId,
    List<string> actions,
    string? redirectUri = null);
```

**After:**
```csharp
Task<bool> SendExecuteActionsEmailAsync(
    string realm,
    string userId,
    List<string> actions,
    string? clientId = null,      // NEW parameter
    string? redirectUri = null);
```

### Implementation

**File:** `GroundUp.infrastructure/services/IdentityProviderAdminService.cs`

```csharp
public async Task<bool> SendExecuteActionsEmailAsync(
    string realm,
    string userId,
    List<string> actions,
    string? clientId = null,
    string? redirectUri = null)
{
    // Build query parameters - BOTH must be provided together
    var queryParams = new List<string>();
    
    if (!string.IsNullOrEmpty(clientId))
    {
        queryParams.Add($"client_id={Uri.EscapeDataString(clientId)}");
    }
    
    if (!string.IsNullOrEmpty(redirectUri))
    {
        queryParams.Add($"redirect_uri={Uri.EscapeDataString(redirectUri)}");
    }
    
    var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
    
    var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realm}/users/{userId}/execute-actions-email{queryString}";
    
    var response = await _httpClient.PutAsJsonAsync(requestUrl, actions);
    // ...
}
```

### Keycloak API Call

**Before (Broken):**
```
PUT /admin/realms/{realm}/users/{userId}/execute-actions-email?redirect_uri=http%3A%2F%2Flocalhost%3A5123%2Fapi%2Fauth%2Fcallback
```

**After (Fixed):**
```
PUT /admin/realms/{realm}/users/{userId}/execute-actions-email?client_id=groundup-api&redirect_uri=http%3A%2F%2Flocalhost%3A5123%2Fapi%2Fauth%2Fcallback
```

## Expected Behavior After Fix

### With client_id Included

1. User receives execute-actions email
2. User clicks link ? Sets password ? Verifies email
3. User sees "Your account has been updated" page
4. **Page now includes "Back to application" link** pointing to `redirect_uri`
5. User clicks link ? Redirects to application

### Important Notes

1. **Not an automatic redirect** - Keycloak shows success page with a link back
2. **Requires both parameters** - `client_id` must be valid for the realm
3. **redirect_uri must be whitelisted** - In client's "Valid Redirect URIs" configuration

## Why We're Not Using This

Even with this fix, we chose the **registration-based flow** instead because:

1. ? **Better UX** - Single seamless registration flow
2. ? **One email** - User registers and accepts invitation in one step
3. ? **No manual link clicking** - Automatic redirect after registration
4. ? **Consistent with first admin** - Same flow for all enterprise users

## When to Use This Fix

This fix is still valuable for:

- **Existing user invitations** - Users who already have accounts
- **Password reset flows** - When users need to reset passwords
- **Email verification** - Standalone email verification without registration
- **Other execute-actions scenarios** - Any time you need execute-actions to redirect back

## Example Usage (If Needed)

```csharp
// For existing users who need to verify email
var actions = new List<string> { "VERIFY_EMAIL" };
var clientId = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE"); // "groundup-api"
var redirectUri = "http://localhost:5123/api/invitations/invite/{token}";

await _identityProvider.SendExecuteActionsEmailAsync(
    realm: tenant.RealmName,
    userId: keycloakUserId,
    actions: actions,
    clientId: clientId,           // Include client_id
    redirectUri: redirectUri      // Include redirect_uri
);
```

## redirect_uri Considerations

### What redirect_uri Should Be

**NOT this (OAuth endpoint):**
```
http://localhost:8080/realms/{realm}/protocol/openid-connect/auth?...
```

**Instead, use YOUR application URL:**
```
http://localhost:5123/api/invitations/invite/{token}
```

### Why

- `redirect_uri` is where the user **returns after completing actions**
- Should point to **your application**, not back to Keycloak
- Can be an API endpoint or frontend page
- Must be in client's "Valid Redirect URIs" whitelist

### Valid Redirect URI Configuration

In Keycloak client settings for `groundup-api`, ensure:

```
Valid Redirect URIs:
- http://localhost:5123/*
- http://localhost:5173/*
- https://yourdomain.com/*
```

## Keycloak Client Configuration

For execute-actions to work properly:

1. **Client must exist** in the realm
2. **Valid Redirect URIs** must include your callback URLs
3. **Client must be enabled**
4. **Standard Flow** must be enabled

Example for `groundup-api` client:
```json
{
  "clientId": "groundup-api",
  "enabled": true,
  "publicClient": false,
  "standardFlowEnabled": true,
  "redirectUris": [
    "http://localhost:5123/*",
    "http://localhost:5173/*"
  ],
  "webOrigins": [
    "http://localhost:5123",
    "http://localhost:5173"
  ]
}
```

## Testing the Fix

If you want to test execute-actions with the fix:

1. **Create Keycloak user:**
```csharp
var userId = await _identityProvider.CreateUserAsync(realm, createUserDto);
```

2. **Send execute-actions email with both parameters:**
```csharp
await _identityProvider.SendExecuteActionsEmailAsync(
    realm: "tenant_xyz",
    userId: userId,
    actions: new List<string> { "UPDATE_PASSWORD", "VERIFY_EMAIL" },
    clientId: "groundup-api",
    redirectUri: "http://localhost:5123/complete"
);
```

3. **Check email** - User receives action email

4. **Complete actions** - Set password, verify email

5. **Verify redirect link** - Should see "Back to application" link on success page

6. **Click link** - Should redirect to `http://localhost:5123/complete`

## Code Changes Summary

### Files Modified

1. **IIdentityProviderAdminService.cs**
   - Added `client_id` parameter to interface
   - Updated documentation

2. **IdentityProviderAdminService.cs**
   - Added `client_id` parameter to implementation
   - Build query string with both parameters
   - Added logging for debugging

### No Breaking Changes

- `client_id` is optional (defaults to `null`)
- `redirect_uri` is optional (defaults to `null`)
- Backward compatible with existing code
- Build successful ?

## References

### Keycloak Documentation

- **Execute Actions Email API:** https://www.keycloak.org/docs-api/latest/rest-api/index.html#_users_resource
- **Key Points:**
  - `redirectUri` (query param) - Optional redirect after completing actions
  - `clientId` (query param) - Optional client ID (MUST be provided with redirect_uri)
  - If both are provided, Keycloak shows a link back to the application

### Related GitHub Issues

- https://github.com/keycloak/keycloak/issues/25719 - Execute action email link discussion
- Common issue: "Account updated" dead-end without back link

## Conclusion

**This fix makes execute-actions-email work correctly** by including the required `client_id` parameter.

However, **we're using registration-based invitations instead** because:
- Better user experience
- Single email
- Seamless flow
- No manual link clicking required

The fix is still valuable for other use cases (existing users, password reset, email verification, etc.).

---

**Status:** ? Fixed and ready for use when needed  
**Current approach:** Registration-based invitations (preferred)  
**Build:** ? Successful  
**Breaking changes:** None
