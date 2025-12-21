# Enterprise Invitation: Registration-Based Flow (ONE EMAIL SOLUTION)

## Overview

**Problem Solved:** Keycloak's execute-actions email doesn't support post-action redirects, requiring two separate emails.

**New Solution:** Use Keycloak's **registration flow** instead of pre-creating users. Users self-register, and the invitation token is validated during OAuth callback.

## How It Works

### Old Flow (Two Emails)
```
1. Admin creates invitation
2. System creates Keycloak user
3. System sends execute-actions email (password setup)
4. User sets password ? STUCK on "Account updated" page
5. System sends second email with invitation link
6. User clicks invitation link ? OAuth login ? Invitation accepted
```

**Problems:**
- Two emails confusing
- User stuck after password setup
- Keycloak redirect_uri doesn't work

### New Flow (ONE Email)
```
1. Admin creates invitation
2. System stores invitation in database (NO Keycloak user created)
3. System sends ONE email with invitation link
4. User clicks link ? Redirected to Keycloak REGISTRATION page
5. User fills registration form ? Keycloak creates user
6. Keycloak redirects to OAuth callback with invitation token in state
7. System validates invitation ? Accepts invitation ? User has tenant access
```

**Benefits:**
- ? ONE email (not two)
- ? No "stuck" state - registration flows directly to callback
- ? User self-registers (better UX)
- ? No Keycloak execute-actions issues
- ? Works exactly like first admin flow

## Technical Changes

### 1. Invitation Creation

**Before:**
```csharp
// Create Keycloak user
var keycloakUserId = await _identityProvider.CreateUserAsync(realm, createUserDto);

// Send execute-actions email
await _identityProvider.SendExecuteActionsEmailAsync(
    realm,
    keycloakUserId,
    ["UPDATE_PASSWORD", "VERIFY_EMAIL"],
    redirectUri
);
```

**After:**
```csharp
// Don't create Keycloak user - just store invitation
_logger.LogInformation($"Invitation created for new user. User will register via Keycloak registration form");
_logger.LogInformation($"Invitation URL: /api/invitations/invite/{invitation.InvitationToken}");

// TODO: Send ONE email with invitation link
// Link: GET /api/invitations/invite/{token}
```

### 2. Invitation Acceptance Endpoint

**Before (Login URL):**
```csharp
var authUrl = $"{keycloakAuthUrl}/realms/{realm}/protocol/openid-connect/auth?...";
```

**After (Registration URL):**
```csharp
var registrationUrl = $"{keycloakAuthUrl}/realms/{realm}/protocol/openid-connect/registrations?...";
```

**Key Change:** Use `/registrations` endpoint instead of `/auth` endpoint

### 3. OAuth Callback

**No changes needed!** The callback already:
- Extracts invitation token from state
- Validates invitation
- Creates user in database
- Accepts invitation
- Grants tenant access

## User Experience

### Email Template

```html
Subject: You're invited to join {TenantName}!

Hi,

You've been invited to join {TenantName} on GroundUp.

Click the button below to create your account and accept this invitation:

[Create Account & Accept Invitation]
? https://api.groundup.com/api/invitations/invite/{token}

This link will take you to a secure registration page where you can
create your account. After registration, you'll automatically gain
access to {TenantName}.

This invitation expires in 7 days.

Questions? Contact {AdminEmail}
```

### User Journey

1. **User receives email** ? Clicks "Create Account & Accept Invitation"
2. **Redirects to:** `GET /api/invitations/invite/{token}`
3. **API validates invitation** ? Generates registration URL ? Redirects
4. **User sees:** Keycloak registration form
5. **User fills:** Email, password, first name, last name
6. **User submits** ? Keycloak creates account
7. **Keycloak redirects to:** `GET /api/auth/callback?code=...&state=...`
8. **API callback:**
   - Exchanges code for tokens
   - Detects invitation flow from state
   - Creates User record in database
   - Accepts invitation
   - Creates UserTenant record
   - Returns JWT with tenant access
9. **User is logged in** ? Has access to tenant ? Done!

## Implementation Steps

### Step 1: Update Invitation Creation (Done ?)

**File:** `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs`

Changed to NOT create Keycloak users. Just store invitation in database.

### Step 2: Update Invitation Endpoint (Done ?)

**File:** `GroundUp.api/Controllers/InvitationController.cs`

Changed `GET /api/invitations/invite/{token}` to return **registration** URL instead of login URL.

### Step 3: Implement Email Service (TODO)

**File:** `GroundUp.infrastructure/services/EmailService.cs` (create)

```csharp
public async Task<bool> SendInvitationEmailAsync(
    string email,
    string invitationToken,
    string tenantName,
    string invitationUrl)
{
    var subject = $"You're invited to join {tenantName}!";
    var body = BuildInvitationEmailHtml(email, tenantName, invitationUrl);
    return await SendEmailAsync(email, subject, body);
}
```

### Step 4: Send Email After Invitation Creation (TODO)

**File:** `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs`

```csharp
// After creating invitation
var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5123";
var invitationUrl = $"{apiUrl}/api/invitations/invite/{invitation.InvitationToken}";

await _emailService.SendInvitationEmailAsync(
    dto.Email,
    invitation.InvitationToken,
    tenant.Name,
    invitationUrl
);
```

### Step 5: Test End-to-End (TODO)

1. Create invitation
2. Check email
3. Click link
4. Register account
5. Verify auto-login and tenant access

## Advantages Over Previous Approaches

| Approach | Emails | User Stuck? | Keycloak Custom? | Complexity |
|----------|--------|-------------|------------------|------------|
| Execute-actions + redirect_uri | 1 | Yes ? | No | Low |
| Execute-actions + follow-up email | 2 | No | No | Medium |
| Execute-actions + custom template | 1 | No | Yes ? | High |
| **Registration-based (NEW)** | **1** | **No** | **No** | **Low** |

## Security Considerations

### Invitation Token Validation

1. **Token in database** - Invitation must exist and be pending
2. **Expiration check** - Invitation must not be expired
3. **Email match** - User's registration email must match invitation email
4. **Single use** - Invitation marked as accepted after first use

### Registration Security

Keycloak handles:
- Password strength validation
- Email verification (if configured)
- Rate limiting on registration
- CAPTCHA (if configured)

### OAuth Security

Standard OAuth 2.0 Authorization Code flow:
- State parameter prevents CSRF
- Code is single-use
- Token exchange requires client secret
- Short-lived access tokens

## Edge Cases

### Case 1: User Already Exists in Keycloak

**Scenario:** Admin invites user, but user already registered in the realm.

**Current behavior:**
```csharp
if (existingUserId != null)
{
    _logger.LogInformation($"User already exists in Keycloak");
    // No email sent - admin must manually share invitation link
}
```

**Recommended:** Change invitation URL from `/registrations` to `/auth` for existing users:

```csharp
// In InvitationController.InviteRedirect
var existingUser = await _identityProvider.GetUserIdByEmailAsync(realm, invitation.Email);
var endpoint = existingUser != null 
    ? "/protocol/openid-connect/auth"          // Login for existing users
    : "/protocol/openid-connect/registrations"; // Registration for new users

var authUrl = $"{keycloakAuthUrl}/realms/{realm}{endpoint}{oauthParams}";
```

### Case 2: Email Verification Required

**Scenario:** Realm has `verifyEmail=true`, user must verify email before access.

**Behavior:**
1. User registers
2. Keycloak sends verification email
3. User clicks verification link
4. Keycloak marks email as verified
5. User redirected to OAuth callback
6. Invitation accepted, access granted

**No changes needed** - Keycloak handles this automatically.

### Case 3: Invitation Expired

**Scenario:** User clicks invitation link after expiration.

**Behavior:**
```csharp
if (invitation.ExpiresAt < DateTime.UtcNow)
{
    return BadRequest(new ApiResponse<AuthUrlResponseDto>(
        null,
        false,
        "Invitation has expired",
        ...
    ));
}
```

User sees error message. Admin must create new invitation.

### Case 4: User Registers with Different Email

**Scenario:** User clicks invitation link, but registers with different email.

**Behavior:**
1. User redirects to registration page
2. User changes email field (Keycloak allows this)
3. User registers with wrong email
4. OAuth callback receives user data
5. **Email mismatch check fails** in `AcceptInvitationAsync`

```csharp
if (!user.Email.Equals(invitation.ContactEmail, StringComparison.OrdinalIgnoreCase))
{
    return new ApiResponse<bool>(
        false,
        false,
        "Email mismatch - this invitation is for a different email address"
    );
}
```

**Solution:** Pre-fill registration form with invitation email (Keycloak supports this via `login_hint` parameter):

```csharp
var registrationUrl = $"{keycloakAuthUrl}/realms/{realm}/protocol/openid-connect/registrations" +
                     $"?login_hint={Uri.EscapeDataString(invitation.ContactEmail)}" +
                     $"&{oauthParams}";
```

## Testing

### Manual Test Steps

1. **Create invitation:**
```bash
POST http://localhost:5123/api/tenant-invitations
Authorization: Bearer {admin-jwt}
Content-Type: application/json

{
  "email": "newuser@example.com",
  "isAdmin": false,
  "expirationDays": 7
}
```

2. **Get invitation URL from response:**
```json
{
  "data": {
    "id": 1,
    "invitationToken": "abc123..."
  }
}
```

3. **Test invitation endpoint:**
```bash
GET http://localhost:5123/api/invitations/invite/abc123...
```

Should return:
```json
{
  "data": {
    "authUrl": "http://localhost:8080/realms/tenant_xyz/protocol/openid-connect/registrations?...",
    "action": "invitation"
  }
}
```

4. **Open registration URL in browser**
5. **Fill registration form:**
   - Email: newuser@example.com
   - Password: SecurePassword123!
   - First name: New
   - Last name: User
6. **Submit form**
7. **Should redirect to:** `http://localhost:5123/api/auth/callback?code=...&state=...`
8. **Should see:** User logged in, JWT with tenant access

### Verify in Database

```sql
-- Check invitation accepted
SELECT * FROM TenantInvitations WHERE InvitationToken = 'abc123...';
-- Status should be 'Accepted', AcceptedAt should be set

-- Check user created
SELECT * FROM Users WHERE Email = 'newuser@example.com';

-- Check tenant assignment
SELECT * FROM UserTenants WHERE UserId = (SELECT Id FROM Users WHERE Email = 'newuser@example.com');
```

## Environment Variables

```bash
# Keycloak Configuration
KEYCLOAK_AUTH_SERVER_URL=http://localhost:8080
KEYCLOAK_RESOURCE=groundup-api
KEYCLOAK_SECRET=your_client_secret
KEYCLOAK_ADMIN_CLIENT_ID=admin-cli
KEYCLOAK_ADMIN_CLIENT_SECRET=your_admin_secret

# API Configuration
API_URL=http://localhost:5123

# Email Configuration (for sending invitation emails)
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_USERNAME=your_smtp_username
SMTP_PASSWORD=your_smtp_password
SMTP_FROM=noreply@groundup.com
SMTP_FROM_DISPLAY_NAME=GroundUp
```

## Next Steps

1. ? **Update invitation creation** - Done (no Keycloak user creation)
2. ? **Update invitation endpoint** - Done (registration URL)
3. ?? **Implement email service** - Need to create EmailService
4. ?? **Send invitation email** - Call email service after invitation creation
5. ?? **Add `login_hint` parameter** - Pre-fill email in registration form
6. ?? **Handle existing users** - Use login URL instead of registration URL
7. ?? **Test end-to-end** - Verify full flow works

## Comparison with First Admin Flow

This flow is **identical** to the first enterprise admin registration flow:

**First Admin:**
```
1. POST /api/tenants/signup (create tenant + realm)
2. Returns registration URL
3. User registers
4. OAuth callback creates user + tenant assignment
```

**Invited User:**
```
1. POST /api/tenant-invitations (create invitation)
2. GET /api/invitations/invite/{token} (returns registration URL)
3. User registers
4. OAuth callback creates user + accepts invitation + tenant assignment
```

**Key difference:** Invitation token is in OAuth state for validation during callback.

## Conclusion

This registration-based approach solves the execute-actions redirect issue by:

1. ? **One email** instead of two
2. ? **No stuck state** - registration flows seamlessly to callback
3. ? **Familiar flow** - same as first admin registration
4. ? **No Keycloak customization** - uses standard registration endpoint
5. ? **Better UX** - single action to register and accept invitation

**Implementation time:** ~60 minutes (just need to add email service)

**Ready to implement?** Start with Step 3 (email service) in the implementation section above.
