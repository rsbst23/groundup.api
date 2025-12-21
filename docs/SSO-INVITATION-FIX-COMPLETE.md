# SSO Invitation Fix - Complete Summary

## Problem Identified

When inviting users to enterprise tenants configured with Google SSO, the system was **pre-creating Keycloak users** before the user authenticated. This caused:

1. ? "User already exists" conflicts when clicking "Sign in with Google"
2. ? User had to "link to existing account" instead of seamless SSO
3. ? Confusion between local accounts and SSO accounts

## Root Cause

`TenantInvitationRepository.AddAsync` was **always** calling `CreateUserAsync` for enterprise invitations, regardless of authentication method.

```csharp
// OLD CODE (WRONG)
if (tenant.TenantType == Enterprise && !string.IsNullOrEmpty(tenant.RealmName))
{
    // Always creates user - even for SSO! ?
    await _identityProvider.CreateUserAsync(tenant.RealmName, createUserDto);
}
```

This is correct for **local accounts** (password-based), but **wrong for SSO** (Google, Azure AD, etc.).

---

## Solution Implemented

### 1. Added `IsLocalAccount` Flag

**File**: `GroundUp.core/dtos/TenantInvitationDtos.cs`

```csharp
public class CreateTenantInvitationDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
    
    public bool IsAdmin { get; set; } = false;
    public int ExpirationDays { get; set; } = 7;
    
    /// <summary>
    /// TRUE: Local account (creates Keycloak user + sends password email)
    /// FALSE: SSO account (no user creation, let SSO provider handle it)
    /// </summary>
    public bool IsLocalAccount { get; set; } = true; // Default: backward compatible
}
```

### 2. Updated Invitation Logic

**File**: `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs`

```csharp
// NEW CODE (CORRECT)
if (dto.IsLocalAccount && tenant.TenantType == Enterprise && !string.IsNullOrEmpty(tenant.RealmName))
{
    _logger.LogInformation("Processing LOCAL ACCOUNT invitation...");
    
    // Only create Keycloak user for local accounts
    await _identityProvider.CreateUserAsync(tenant.RealmName, createUserDto);
    await _identityProvider.SendExecuteActionsEmailAsync(...);
}
else if (!dto.IsLocalAccount && tenant.TenantType == Enterprise)
{
    _logger.LogInformation("Processing SSO invitation...");
    _logger.LogInformation("Skipping Keycloak user creation - SSO provider will create user");
}
```

---

## Flows Comparison

### Local Account Flow (Password-Based)

```
Admin creates invitation (isLocalAccount: true)
  ?
? Keycloak user created immediately
  ?
? Execute-actions email sent (UPDATE_PASSWORD, UPDATE_PROFILE, VERIFY_EMAIL)
  ?
User clicks email link ? Sets password ? Completes profile
  ?
User clicks "Back to application" ? Invitation acceptance
  ?
? Tenant assignment completed
```

**Use Cases**:
- User doesn't have SSO account
- User prefers password authentication
- Internal testing/development

---

### SSO Flow (Google, Azure AD, etc.)

```
Admin creates invitation (isLocalAccount: false)
  ?
? NO Keycloak user created (invitation stored only)
  ?
User opens invitation URL in browser
  ?
Redirects to Keycloak login ? Shows "Sign in with Google"
  ?
User clicks Google button ? Google OAuth flow
  ?
? Keycloak creates user automatically from Google
  ?
Redirects to API callback ? UserSyncMiddleware syncs to DB
  ?
? Tenant assignment completed
```

**Use Cases**:
- User has Gmail/Google Workspace account
- Enterprise SSO configured
- Higher security (no password to manage)

---

## API Usage

### Create Local Account Invitation

```http
POST http://localhost:5123/api/invitations
Authorization: Bearer {token}
Content-Type: application/json

{
  "email": "user@example.com",
  "isAdmin": false,
  "expirationDays": 7,
  "isLocalAccount": true
}
```

**Result**: User created in Keycloak + email sent

---

### Create SSO Invitation

```http
POST http://localhost:5123/api/invitations
Authorization: Bearer {token}
Content-Type: application/json

{
  "email": "user@gmail.com",
  "isAdmin": false,
  "expirationDays": 7,
  "isLocalAccount": false  ? KEY DIFFERENCE
}
```

**Result**: No Keycloak user created + smooth SSO flow

---

## Testing the Fix

### Quick Test

```powershell
# Run test script
.\scripts\test-sso-local-invitations.ps1
```

### Manual Test

**Step 1**: Create SSO invitation with `isLocalAccount: false`

**Step 2**: Check Keycloak admin console
- Go to: http://localhost:8080/admin
- Navigate to your enterprise realm
- Go to: Users
- ? **Verify**: User should NOT exist yet

**Step 3**: Open invitation URL

**Step 4**: Click "Sign in with Google"

**Step 5**: Complete Google OAuth

**Step 6**: Check Keycloak again
- ? **Verify**: User NOW exists
- ? **Verify**: Federated Identity shows Google link

---

## Backward Compatibility

### Default Behavior

`isLocalAccount` defaults to `true` for backward compatibility.

Existing code that doesn't specify the flag:
```json
{
  "email": "user@example.com"
}
```

Will behave as **local account** (same as before the fix).

---

## Migration

### No Database Changes Needed

This fix only affects **new invitation creation**. Existing invitations are not affected.

### Code Migration

If you have frontend code creating invitations:

**Before**:
```typescript
await api.post('/invitations', {
  email: userEmail,
  isAdmin: false
});
```

**After** (to use SSO):
```typescript
await api.post('/invitations', {
  email: userEmail,
  isAdmin: false,
  isLocalAccount: false  // Add this for SSO invitations
});
```

---

## Verification Checklist

After deploying this fix:

### For Local Account Invitations
- [ ] Keycloak user created immediately
- [ ] Execute-actions email sent
- [ ] User can set password via email
- [ ] User can accept invitation
- [ ] Tenant assignment works

### For SSO Invitations
- [ ] NO Keycloak user created upfront
- [ ] NO "user already exists" error
- [ ] "Sign in with Google" appears on login page
- [ ] Google OAuth completes smoothly
- [ ] User created by Google provider
- [ ] Federated identity link shows in Keycloak
- [ ] Tenant assignment works

---

## Troubleshooting

### Issue: Still getting "user already exists"

**Cause**: User was created before with local account

**Fix**: 
1. Delete user in Keycloak admin console
2. Recreate invitation with `isLocalAccount: false`

---

### Issue: Local account not sending email

**Cause**: SMTP not configured in realm

**Fix**: 
1. Configure SMTP in Keycloak realm settings
2. OR switch to SSO invitation (`isLocalAccount: false`)

---

### Issue: SSO user not assigned to tenant

**Cause**: Callback didn't trigger or invitation expired

**Fix**:
1. Check invitation status in database
2. Check API logs for callback errors
3. Verify invitation token is still valid

---

## Files Changed

1. ? `GroundUp.core/dtos/TenantInvitationDtos.cs` - Added `IsLocalAccount` flag
2. ? `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs` - Updated invitation logic

---

## Next Steps

### Recommended Improvements

1. **Frontend UI**: Add account type selector when creating invitations
   ```tsx
   <select>
     <option value="local">Local Account (Password)</option>
     <option value="sso">SSO (Google, Azure AD)</option>
   </select>
   ```

2. **Auto-Detection**: Detect SSO capability from email domain
   ```typescript
   const isLocalAccount = !email.endsWith('@gmail.com') && !email.endsWith('@microsoft.com');
   ```

3. **Tenant Settings**: Allow admins to set default invitation type per tenant
   ```json
   {
     "defaultInvitationType": "sso"
   }
   ```

---

## Summary

? **Problem Fixed**: No more pre-created users for SSO invitations
? **Smooth SSO Flow**: Users can authenticate via Google without conflicts  
? **Backward Compatible**: Existing local account flow unchanged
? **Flexible**: Support both authentication methods in same tenant

This fix enables true enterprise SSO support! ??

---

## Related Documentation

- [Google SSO Testing Guide](./GOOGLE-SSO-TESTING-GUIDE.md)
- [Google SSO Quick Test](./GOOGLE-SSO-QUICK-TEST.md)
- [SSO vs Local Account Invitations](./SSO-VS-LOCAL-ACCOUNT-INVITATIONS-FIX.md)
