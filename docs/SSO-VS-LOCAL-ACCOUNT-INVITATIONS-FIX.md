# SSO vs Local Account Invitations - Fix Summary

## Problem Fixed

**Before**: When creating invitations, Keycloak users were **always created upfront**, causing conflicts when users authenticated via SSO (Google, Azure AD, etc.)

**After**: Invitations can now specify `IsLocalAccount` flag to differentiate between:
- **Local accounts** (password-based) - User created in Keycloak + execute-actions email sent
- **SSO accounts** (Google, Azure AD) - No Keycloak user created; SSO provider creates user upon first login

---

## API Changes

### CreateTenantInvitationDto

```csharp
{
  "email": "user@example.com",
  "isAdmin": false,
  "expirationDays": 7,
  "isLocalAccount": true  // NEW FIELD
}
```

**IsLocalAccount Property**:
- `true` (default) - Local account invitation (password-based)
  - Creates Keycloak user immediately
  - Sends execute-actions email (UPDATE_PASSWORD, UPDATE_PROFILE, VERIFY_EMAIL)
  - User sets password via email link
  
- `false` - SSO invitation (Google, Azure AD, etc.)
  - Does NOT create Keycloak user
  - User authenticates via SSO provider
  - Keycloak creates user automatically from SSO provider
  - Tenant assignment happens after SSO authentication

---

## Testing Scenarios

### Scenario 1: Local Account Invitation (Password-Based)

**Create Invitation**:
```http
POST http://localhost:5123/api/invitations
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "email": "localuser@example.com",
  "isAdmin": false,
  "expirationDays": 7,
  "isLocalAccount": true  ? Local account
}
```

**Expected Flow**:
1. ? Keycloak user created immediately
2. ? Execute-actions email sent to `localuser@example.com`
3. User clicks email link ? Sets password ? Completes profile
4. User clicks "Back to application" ? Redirected to invitation acceptance
5. Tenant assignment completed

**Verify in Keycloak**:
- Admin Console ? Users ? See `localuser@example.com`
- Federated Identity: None (local account)

---

### Scenario 2: SSO Invitation (Google)

**Create Invitation**:
```http
POST http://localhost:5123/api/invitations
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "email": "ssouser@gmail.com",
  "isAdmin": false,
  "expirationDays": 7,
  "isLocalAccount": false  ? SSO account
}
```

**Expected Flow**:
1. ? NO Keycloak user created
2. ? Invitation stored in database
3. User opens invitation URL in browser
4. Redirects to Keycloak login for realm
5. User clicks "Sign in with Google"
6. Google OAuth completes
7. **Keycloak creates user automatically** from Google profile
8. Redirects to your API callback
9. UserSyncMiddleware syncs user to database
10. Tenant assignment completed

**Verify in Keycloak**:
- Admin Console ? Users ? See `ssouser@gmail.com`
- **Federated Identity**: Link to Google ?
- Created **after** user authenticated, not upfront

---

## Backward Compatibility

**Default behavior**: `isLocalAccount: true`

If you don't specify `isLocalAccount` in the API request:
- Defaults to `true` (local account)
- Maintains backward compatibility with existing invitation flows

---

## Migration Guide

### For Existing Invitations

No database migration needed! The flag only affects **new invitation creation**.

Existing pending invitations will continue to work as before (local account behavior).

---

## How to Test the Fix

### Test 1: Local Account Flow (Unchanged)

```bash
# 1. Create local account invitation
POST /api/invitations
{
  "email": "test1@example.com",
  "isLocalAccount": true
}

# 2. Check Keycloak - user should exist immediately
# 3. Check email - execute-actions email sent
# 4. User sets password via email link
# 5. User accepts invitation
```

**Expected**: Works exactly as before

---

### Test 2: SSO Flow (Fixed!)

```bash
# 1. Create SSO invitation
POST /api/invitations
{
  "email": "test2@gmail.com",
  "isLocalAccount": false  ? New!
}

# 2. Check Keycloak - user should NOT exist yet
# 3. Open invitation URL in browser
# 4. Click "Sign in with Google"
# 5. Complete Google OAuth
# 6. Check Keycloak - user NOW exists with Google federated identity
```

**Expected**: 
- ? No pre-created user
- ? No "user already exists" conflict
- ? Smooth Google SSO flow
- ? User created by Google provider
- ? Tenant assignment completed

---

## Logs to Watch

### Local Account Invitation
```
Processing LOCAL ACCOUNT invitation for enterprise tenant...
User does not exist in realm, creating new LOCAL ACCOUNT user...
Successfully created Keycloak LOCAL ACCOUNT user {userId}
Sending execute actions email...
```

### SSO Invitation
```
Processing SSO invitation for enterprise tenant...
Skipping Keycloak user creation - user will authenticate via SSO
User will be created automatically by the SSO provider upon first login
```

---

## Frontend Guidance

When creating invitations in the UI, provide a checkbox or dropdown:

```tsx
<label>
  <input 
    type="checkbox" 
    name="isLocalAccount"
    defaultChecked={true}
  />
  Local Account (Password-based)
</label>

<p>
  Uncheck if user will authenticate via SSO (Google, Azure AD, etc.)
</p>
```

Or more explicitly:

```tsx
<select name="accountType">
  <option value="local">Local Account (Password)</option>
  <option value="sso">SSO (Google, Azure AD, etc.)</option>
</select>
```

Map to: `isLocalAccount: accountType === 'local'`

---

## API Response

Invitation response includes all the same fields - no changes to the response structure:

```json
{
  "success": true,
  "data": {
    "id": 123,
    "email": "ssouser@gmail.com",
    "tenantId": 1,
    "invitationToken": "abc123...",
    "status": "Pending",
    "expiresAt": "2024-01-15T12:00:00Z",
    "isAdmin": false,
    "createdAt": "2024-01-08T10:00:00Z",
    "createdByUserName": "Admin User"
  }
}
```

---

## Production Considerations

### When to Use `isLocalAccount: true`
- User doesn't have Google/Azure AD account
- User prefers password-based authentication
- Testing/development environments
- Internal admin accounts

### When to Use `isLocalAccount: false`
- User has Gmail/Google Workspace account
- User has Microsoft/Azure AD account
- Enterprise SSO is configured
- Higher security requirements (no password to manage)

---

## Troubleshooting

### Issue: User still gets "already exists" error with SSO

**Check**:
1. Is `isLocalAccount: false` in the invitation request?
2. Was the user created manually before via local account?
3. Check Keycloak logs for user creation attempts

**Fix**: Delete the pre-created user in Keycloak admin console and retry

---

### Issue: Local account invitation not sending email

**Check**:
1. Is `isLocalAccount: true`?
2. Is SMTP configured in the realm?
3. Check realm SMTP settings in Keycloak

**Fix**: Configure SMTP or use SSO instead

---

## Summary

? **SSO invitations** - No upfront user creation, smooth OAuth flow
? **Local invitations** - User created + execute-actions email (unchanged)  
? **Backward compatible** - Default to local account behavior
? **Flexible** - Support both flows in the same tenant

This fix enables true enterprise SSO support without conflicts! ??
