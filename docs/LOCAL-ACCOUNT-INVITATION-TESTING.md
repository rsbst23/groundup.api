# Testing Local Account Enterprise Invitation Flow

## What We Just Implemented

? **Added 3 new methods to `IIdentityProviderAdminService`:**
1. `GetUserIdByEmailAsync` - Check if user exists in Keycloak
2. `CreateUserAsync` - Create new Keycloak user with required actions
3. `SendExecuteActionsEmailAsync` - Send Keycloak's execute actions email

? **Updated `TenantInvitationRepository.AddAsync`:**
- Automatically creates Keycloak user when invitation is created (for enterprise tenants)
- Sends ONE email from Keycloak with password setup link
- Handles existing users (sends email verification if needed)

## How It Works Now

### When Admin Creates Invitation:

```json
POST /api/invitations
Headers: Authorization: Bearer {admin_token}, TenantId: {enterprise_tenant_id}
Body:
{
  "email": "newuser@acme.com",
  "isAdmin": false,
  "expirationDays": 7
}
```

**What happens automatically:**

1. ? Creates invitation record in database
2. ? Checks if user exists in Keycloak realm
3. ? If not exists: Creates Keycloak user with:
   - Username: email prefix (e.g., "newuser")
   - Email: newuser@acme.com
   - Enabled: true
   - EmailVerified: false
   - Required Actions: UPDATE_PASSWORD, VERIFY_EMAIL
4. ? Sends execute actions email from Keycloak
5. ? Returns invitation details to admin

### What the Invited User Experiences:

1. **Receives ONE email from Keycloak** with subject like "Set up your account"
2. **Clicks link in email** ? Taken to Keycloak
3. **Sets password** ? Keycloak validates password strength
4. **Verifies email** ? Keycloak marks email as verified
5. **Redirected to tenant frontend** (if configured)
6. **Logs in with new credentials**
7. **Auth callback processes invitation** ? User gets access to tenant

## Testing Steps

### Prerequisites:
- Enterprise tenant exists with dedicated realm
- SMTP configured in Keycloak (or check Keycloak logs for email content)
- Admin user authenticated and has invitation permission

### Test 1: New User (Fresh Invitation)

```bash
# 1. Create invitation
POST /api/invitations
Authorization: Bearer {admin_token}
TenantId: 1
{
  "email": "bob@acme.com",
  "isAdmin": false,
  "expirationDays": 7
}

# Expected response:
{
  "data": {
    "id": 5,
    "email": "bob@acme.com",
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "realmName": "tenant_acme_a1b2",
    "status": "Pending",
    "expiresAt": "2024-01-17T00:00:00Z",
    "isAdmin": false,
    "invitationToken": "abc123..."
  },
  "success": true
}

# 2. Check Keycloak Admin UI
# - User "bob" should exist in realm "tenant_acme_a1b2"
# - Required actions: UPDATE_PASSWORD, VERIFY_EMAIL
# - Enabled: true
# - Email verified: false

# 3. Check email (or Keycloak server logs)
# - Email sent to bob@acme.com
# - Contains link to Keycloak execute actions page
# - Link includes redirect_uri (tenant frontend)

# 4. User clicks email link
# - Sets password
# - Verifies email
# - Redirected to tenant frontend

# 5. User logs in
# - Authenticates with new credentials
# - Auth callback processes invitation
# - User gains access to tenant
```

### Test 2: Existing User (Already in Keycloak)

```bash
# 1. Manually create user in Keycloak Admin UI first
# - Realm: tenant_acme_a1b2
# - Username: alice
# - Email: alice@acme.com
# - Enabled: true
# - Email verified: false

# 2. Create invitation for same email
POST /api/invitations
{
  "email": "alice@acme.com",
  "isAdmin": false,
  "expirationDays": 7
}

# Expected behavior:
# - Does NOT create duplicate user
# - Sends VERIFY_EMAIL action (if email not verified)
# - Returns invitation record
```

### Test 3: Existing Verified User

```bash
# 1. User exists with email verified: true

# 2. Create invitation
POST /api/invitations
{
  "email": "verified@acme.com",
  "isAdmin": false,
  "expirationDays": 7
}

# Expected behavior:
# - Does NOT create duplicate user
# - Does NOT send email (user already verified)
# - Returns invitation record
# - User can log in directly and accept invitation
```

## What to Check

### In Logs:
```
Creating invitation for bob@acme.com to tenant 1 by user {guid}
Processing local account invitation for enterprise tenant Acme Corporation in realm tenant_acme_a1b2
Searching for user by email in realm tenant_acme_a1b2: bob@acme.com
No user found with email bob@acme.com in realm tenant_acme_a1b2
Creating user in realm tenant_acme_a1b2: bob@acme.com
Successfully created user {keycloak_id} in realm tenant_acme_a1b2
Sending execute actions email to user {keycloak_id} in realm tenant_acme_a1b2
Actions: UPDATE_PASSWORD, VERIFY_EMAIL
Redirect URI: https://acme.yourapp.com
Successfully sent execute actions email for invitation 5
Created invitation ID 5 with token abc123...
```

### In Keycloak Admin UI:
1. Go to Realm ? Users
2. Find user by email
3. Check:
   - ? User exists
   - ? Enabled = true
   - ? Email verified = false (until user completes actions)
   - ? Required actions = UPDATE_PASSWORD, VERIFY_EMAIL
   - ? Credentials tab shows NO password set yet

### In Email:
1. Subject: "Set up your account" or similar
2. Body contains:
   - Link to Keycloak execute actions page
   - Instructions to set password and verify email
   - Redirect URI (optional)

## Common Issues

### Issue: "Failed to create Keycloak user"
**Cause:** SMTP not configured or Keycloak admin credentials wrong  
**Solution:** 
- Check KEYCLOAK_ADMIN_CLIENT_ID env var
- Check KEYCLOAK_ADMIN_CLIENT_SECRET env var
- Check admin token acquisition in logs

### Issue: "Failed to send execute actions email"
**Cause:** SMTP not configured in Keycloak  
**Solution:**
- Configure SMTP in Keycloak realm settings
- OR check Keycloak server logs for email content
- OR manually send password reset from Keycloak admin UI

### Issue: User doesn't receive email
**Cause:** SMTP misconfigured  
**Solution:**
- Check Keycloak server logs for email errors
- Verify SMTP settings in realm
- Test email from Keycloak admin UI

### Issue: User can't log in after setting password
**Cause:** Invitation not accepted yet  
**Solution:**
- User needs to authenticate first
- Auth callback will process invitation
- Check invitation status in database

## Next Steps

Once this works, we can:
1. ? Add SSO account flow (no Keycloak user creation)
2. ? Add `accountType` field to distinguish flows
3. ? Add token hashing for security
4. ? Customize email template to include invitation token
5. ? Add invitation acceptance tracking

## Success Criteria

? Admin creates invitation  
? System creates Keycloak user automatically  
? User receives ONE email from Keycloak  
? User sets password and verifies email  
? User logs in with new credentials  
? Auth callback processes invitation  
? User gains access to tenant  
? No errors in logs  
? Invitation status changes to "Accepted"

## Database State After Success

```sql
-- Users table
SELECT * FROM Users WHERE Email = 'bob@acme.com';
-- Result: User record exists

-- UserTenants table
SELECT * FROM UserTenants WHERE UserId = {user_id} AND TenantId = 1;
-- Result: Membership record exists with ExternalUserId = Keycloak sub

-- TenantInvitations table
SELECT * FROM TenantInvitations WHERE ContactEmail = 'bob@acme.com';
-- Result: Status = 'Accepted', AcceptedAt = timestamp, AcceptedByUserId = {user_id}
```

## Notes

- This implementation does NOT require database schema changes
- Works immediately with existing `TenantInvitation` entity
- Only affects enterprise tenants (standard tenants unchanged)
- SMTP must be configured for Keycloak to send emails
- Gracefully handles errors (invitation still created even if Keycloak fails)
- Admin can manually create users in Keycloak if needed
