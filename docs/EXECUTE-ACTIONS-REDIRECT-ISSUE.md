# Execute Actions Email Redirect Issue - SOLVED WITH REGISTRATION FLOW

## Problem Summary (Original)

When creating an enterprise tenant invitation, the system:
1. ? Creates Keycloak user successfully
2. ? Sends execute actions email successfully
3. ? User receives email and can set password
4. ? **After setting password, user is NOT redirected** - stays on "Your account has been updated" page

## Root Cause

**Keycloak's `execute-actions-email` API does NOT support automatic post-action redirects.**

The `redirect_uri` parameter passed to `execute-actions-email` is stored in the action token but is NOT used to automatically redirect users after they complete the required actions. This is by design in Keycloak - the execute-actions flow is meant ONLY for account setup, not for application onboarding.

## FINAL SOLUTION: Registration-Based Invitations ?

**Approach:** Use Keycloak's **registration flow** instead of pre-creating users with execute-actions.

### Why This is Better

| Approach | Emails | Stuck State? | Keycloak Custom? | Complexity |
|----------|--------|--------------|------------------|------------|
| Execute-actions + redirect | 1 | Yes ? | No | Low |
| Execute-actions + follow-up email | 2 | No | No | Medium |
| Execute-actions + custom template | 1 | No | Yes ? | High |
| **Registration-based (FINAL)** | **1** | **No** | **No** | **Low** |

### New Flow

```
1. Admin creates invitation
   ?? Invitation stored in database (NO Keycloak user created)

2. Send ONE email with invitation link
   ?? Link: GET /api/invitations/invite/{token}

3. User clicks link
   ?? Redirected to: Keycloak REGISTRATION page

4. User registers (fills form, sets password)
   ?? Keycloak creates user account

5. Keycloak redirects to OAuth callback
   ?? Invitation token in state parameter

6. API validates invitation and accepts it
   ?? User created in database
   ?? UserTenant record created
   ?? JWT returned with tenant access

7. User is logged in with full access ?
```

### Key Changes

**1. Invitation Creation** - Don't create Keycloak users:
```csharp
// OLD: Pre-create user and send execute-actions email
var keycloakUserId = await _identityProvider.CreateUserAsync(realm, createUserDto);
await _identityProvider.SendExecuteActionsEmailAsync(realm, keycloakUserId, actions);

// NEW: Just store invitation, let user self-register
_logger.LogInformation($"Invitation created. User will self-register via Keycloak");
// TODO: Send email with invitation link
```

**2. Invitation Endpoint** - Return registration URL:
```csharp
// OLD: Login URL
var authUrl = $"{keycloak}/realms/{realm}/protocol/openid-connect/auth?...

// NEW: Registration URL
var registrationUrl = $"{keycloak}/realms/{realm}/protocol/openid-connect/registrations?...";
```

**3. OAuth Callback** - No changes needed! Already validates invitation token from state.

### Benefits

? **ONE email** - Not two separate emails  
? **No stuck state** - Registration flows directly to callback  
? **Familiar UX** - Same as first admin registration  
? **No Keycloak customization** - Uses standard registration endpoint  
? **Simple implementation** - Just need to add email service  

### Implementation Status

- ? Updated `TenantInvitationRepository` to NOT create Keycloak users
- ? Updated `InvitationController` to return registration URLs
- ?? Need to implement email service
- ?? Need to send invitation email after creation

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
```

## Previous Solutions Considered (Rejected)

### Option 1: Follow-Up Email (Rejected - Too Complex)

Send two emails:
1. Execute-actions email (password setup)
2. Follow-up email (invitation link)

**Why rejected:** Two emails are confusing. Registration approach is simpler.

### Option 2: Custom Email Template (Rejected - Too Complex)

Customize Keycloak email template to include invitation link.

**Why rejected:** Requires Keycloak container customization. Registration approach is simpler.

### Option 3: UI-Based Flow (Rejected - Unreliable)

Show "Accept Invitation" banner when user logs in.

**Why rejected:** User might miss notification. Registration approach is more explicit.

## Implementation Details

See **docs/REGISTRATION-BASED-INVITATION-FLOW.md** for:
- Detailed implementation steps
- Code examples
- Testing instructions
- Edge case handling
- Email template examples

## Testing

### Quick Test

1. **Create invitation:**
```bash
POST http://localhost:5123/api/tenant-invitations
{
  "email": "test@example.com",
  "isAdmin": false,
  "expirationDays": 7
}
```

2. **Get invitation URL:**
```bash
GET http://localhost:5123/api/invitations/invite/{token}
# Returns registration URL
```

3. **Open URL in browser** - Should see Keycloak registration form

4. **Register** - Fill form and submit

5. **Should redirect to callback** - User automatically logged in with tenant access

6. **Verify in database:**
```sql
SELECT * FROM TenantInvitations WHERE InvitationToken = '{token}';
-- Status = 'Accepted'

SELECT * FROM UserTenants WHERE UserId = ...;
-- User has tenant assignment
```

## Environment Variables

```bash
# Existing
KEYCLOAK_AUTH_SERVER_URL=http://localhost:8080
KEYCLOAK_RESOURCE=groundup-api
KEYCLOAK_ADMIN_CLIENT_ID=admin-cli
KEYCLOAK_ADMIN_CLIENT_SECRET={your_secret}

# New (for invitation emails)
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_USERNAME={your_aws_ses_username}
SMTP_PASSWORD={your_aws_ses_password}
SMTP_FROM=noreply@groundup.com
API_URL=http://localhost:5123
```

## Related Files

- **Implementation Guide:** `docs/REGISTRATION-BASED-INVITATION-FLOW.md`
- **Invitation Repo:** `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs`
- **Invitation Controller:** `GroundUp.api/Controllers/InvitationController.cs`
- **OAuth Callback:** `GroundUp.api/Controllers/AuthController.cs`

## Success Criteria

? User receives ONE invitation email  
? User clicks link and sees registration form  
? User registers and is automatically logged in  
? User has immediate access to tenant  
? No manual steps required  
? No "stuck" state after registration  

## Current Status

? **SOLUTION IMPLEMENTED** - Registration-based flow  
? **CODE CHANGES COMPLETE** - Repository and controller updated  
?? **EMAIL SERVICE NEEDED** - Next step is to implement email sending  
?? **DOCUMENTATION COMPLETE** - Full implementation guide available  

## Next Steps

1. **Implement EmailService** - Create service to send invitation emails
2. **Integrate with invitation creation** - Send email after invitation created
3. **Test end-to-end** - Verify full flow works
4. **Add `login_hint` parameter** - Pre-fill email in registration form
5. **Handle existing users** - Use login URL instead of registration URL

**Estimated time:** 60 minutes

---

**This solution is FINAL and RECOMMENDED.** It's the simplest, most reliable approach that:
- Requires ONE email (not two)
- Uses standard Keycloak features (no customization)
- Matches the first admin registration flow (consistent UX)
- Avoids execute-actions redirect issues entirely
