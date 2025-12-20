# Invitation Flow - UPDATE_PROFILE Fix

## Problem

When users received invitation emails and completed the Keycloak execute-actions flow, they were only prompted to:
1. Set a password (`UPDATE_PASSWORD`)
2. Verify their email (`VERIFY_EMAIL`)

They were **NOT** prompted to enter their first name and last name, resulting in users being created in the database with empty `FirstName` and `LastName` fields.

## Root Cause

In `TenantInvitationRepository.cs`, the execute-actions email was being sent with only two actions:

```csharp
var actions = new List<string> { "UPDATE_PASSWORD", "VERIFY_EMAIL" };
```

## Solution

Added `UPDATE_PROFILE` action to the list of required actions:

```csharp
// UPDATE_PROFILE: Prompts user to enter first name and last name
// UPDATE_PASSWORD: Prompts user to set their password
// VERIFY_EMAIL: Sends email verification (if SMTP configured)
var actions = new List<string> { "UPDATE_PROFILE", "UPDATE_PASSWORD", "VERIFY_EMAIL" };
```

## Updated Flow

Now when a user receives an invitation email, they will:

1. **Click the email link** ? Opens Keycloak execute-actions page
2. **Update Profile** ? Enter first name and last name
3. **Set Password** ? Create their password
4. **Verify Email** ? Click verification link (if SMTP configured)
5. **Click "Back to Application"** ? Redirected to `/api/invitations/invite/{token}`
6. **Login** ? Keycloak login form with email pre-filled
7. **OAuth Callback** ? `/api/auth/callback` processes authentication
8. **Invitation Accepted** ? User synced to database with complete profile information

## File Changed

- `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs`
  - Line ~247: Added `UPDATE_PROFILE` to actions list

## Testing

To test the fix:

1. **Create a new invitation** (old invitations already sent won't be affected)
   ```bash
   POST /api/invitations
   {
     "email": "newuser@example.com",
     "isAdmin": false,
     "expirationDays": 7
   }
   ```

2. **Check email** for execute-actions link

3. **Complete the flow**:
   - Should see form asking for:
     - First Name
     - Last Name
     - Password
   - Click "Submit"
   - Click "Back to Application"
   - Login with email/password
   - Complete OAuth flow

4. **Verify in database**:
   ```sql
   SELECT Id, Email, FirstName, LastName, ExternalUserId
   FROM Users
   WHERE Email = 'newuser@example.com';
   ```
   
   Should see:
   - ? FirstName populated
   - ? LastName populated
   - ? ExternalUserId set (Keycloak sub)

## Keycloak Actions Reference

| Action | Purpose | User Sees |
|--------|---------|-----------|
| `UPDATE_PROFILE` | Collect first/last name | Form with name fields |
| `UPDATE_PASSWORD` | Set initial password | Password creation form |
| `VERIFY_EMAIL` | Email verification | Verification email sent |
| `UPDATE_EMAIL` | Change email address | Email update form |
| `CONFIGURE_TOTP` | Setup 2FA | QR code for authenticator |

## Related Documentation

- [INVITATION-FLOW-SOLUTION-SUMMARY.md](./INVITATION-FLOW-SOLUTION-SUMMARY.md) - Complete invitation flow overview
- [LOCAL-ACCOUNT-INVITATION-IMPLEMENTATION-COMPLETE.md](./LOCAL-ACCOUNT-INVITATION-IMPLEMENTATION-COMPLETE.md) - Original implementation
- [EXECUTE-ACTIONS-CLIENT-ID-FIX.md](./EXECUTE-ACTIONS-CLIENT-ID-FIX.md) - Redirect URI configuration

## Notes

- The `UPDATE_PROFILE` action will show the standard Keycloak profile form
- First name and last name fields are **required** by default in Keycloak
- Users cannot proceed without filling in these fields
- The profile data is synced to our database during the OAuth callback
- Existing users (who already completed the flow) will need their names updated manually if needed
