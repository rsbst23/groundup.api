# Enterprise Invitation Flow - Quick Reference

## The Problem

**Keycloak's execute-actions email doesn't auto-redirect users after password setup.**

After setting their password, users see "Your account has been updated" and get stuck.

## The Solution

**Send TWO emails:**

1. **Keycloak Email** - Password setup (automatic, from Keycloak)
2. **Application Email** - Invitation link (manual, from your app)

## Flow Diagram

```
Admin creates invitation
         ?
Keycloak user created
         ?
?? Email 1: Password Setup (Keycloak)
?    ?
?  User sets password ?
?    ?
?  User sees "Account updated" 
?    ?
?? Email 2: Complete Invitation (Your App)
     ?
   User clicks invitation link
     ?
   OAuth redirect ? Auto-login
     ?
   Invitation accepted ?
     ?
   User gains tenant access ?
```

## Email 1: Keycloak (Automatic)

**From:** Keycloak (via SMTP config in realm)
**To:** Invited user
**Subject:** "Complete the following actions"
**Link:** `http://localhost:8080/realms/{realm}/login-actions/action-token?key=...`
**Actions:** UPDATE_PASSWORD, VERIFY_EMAIL
**Result:** User account exists, password set, email verified

## Email 2: Application (You Implement This)

**From:** Your application (via SMTP)
**To:** Invited user
**Subject:** "Complete Your Invitation to [Tenant Name]"
**Link:** `http://localhost:5123/api/invitations/invite/{invitationToken}`
**Actions:** Click link to complete invitation
**Result:** User logged in, invitation accepted, tenant access granted

## API Endpoints

### Create Invitation (Admin)
```http
POST /api/tenant-invitations
Authorization: Bearer {admin-jwt}

{
  "email": "user@example.com",
  "isAdmin": false,
  "expirationDays": 7
}
```

**What happens:**
1. Keycloak user created in realm
2. Email 1 sent (Keycloak execute-actions)
3. Email 2 sent (Your invitation completion) ? **You need to implement this**

### Invitation Link (Public)
```http
GET /api/invitations/invite/{invitationToken}
```

**What happens:**
1. Validates invitation is pending and not expired
2. Builds OAuth authorization URL with invitation state
3. Returns auth URL to client
4. Client redirects user to Keycloak for login

### OAuth Callback
```http
GET /api/auth/callback?code=...&state=...
```

**What happens:**
1. Exchanges code for tokens
2. Detects invitation flow from state
3. Accepts invitation automatically
4. Creates UserTenant record
5. Returns JWT with tenant access

## User Experience

### Current (Without Email 2)
```
1. User receives password setup email ?
2. User sets password ?
3. User sees "Account updated" 
4. User thinks "Now what?" 
5. User closes browser ?
6. Admin must manually send invitation link ?
```

### Improved (With Email 2)
```
1. User receives password setup email ?
2. User sets password ?
3. User sees "Account updated"
4. User receives invitation email ?
5. User clicks "Complete Invitation" ?
6. User is logged in automatically ?
7. User has tenant access ?
```

## Implementation Checklist

### Phase 1: Understanding (Complete ?)
- [x] Read `EXECUTE-ACTIONS-REDIRECT-ISSUE.md`
- [x] Understand Keycloak limitation
- [x] Review current invitation flow

### Phase 2: Email Service (Next)
- [ ] Create `IEmailService` interface
- [ ] Implement `EmailService` class
- [ ] Register in DI container
- [ ] Configure SMTP environment variables
- [ ] Test email sending

### Phase 3: Integration (After Email Service)
- [ ] Inject `IEmailService` into `TenantInvitationRepository`
- [ ] Call `SendInvitationCompletionEmailAsync` after execute-actions
- [ ] Test end-to-end invitation flow
- [ ] Verify both emails are received
- [ ] Verify user can complete invitation

### Phase 4: Production (Final)
- [ ] Configure AWS SES for production
- [ ] Update environment variables
- [ ] Test with real email addresses
- [ ] Document for users

## Code Snippets

### Email Service Call
```csharp
// In TenantInvitationRepository.AddAsync after execute-actions email
var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5123";
var invitationUrl = $"{apiUrl}/api/invitations/invite/{invitation.InvitationToken}";

await _emailService.SendInvitationCompletionEmailAsync(
    dto.Email,
    invitation.InvitationToken,
    tenant.Name,
    invitationUrl
);
```

### Email Template (Simplified)
```html
<h2>Welcome to {tenantName}!</h2>

<p>After setting your password, click below to complete your invitation:</p>

<a href="{invitationUrl}">Complete Invitation</a>

<p>This link will log you in and grant you access.</p>
```

## Environment Variables

```bash
# Required for Email Service
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_USERNAME=your_smtp_username
SMTP_PASSWORD=your_smtp_password
SMTP_FROM=noreply@yourdomain.com
SMTP_FROM_DISPLAY_NAME=GroundUp
API_URL=http://localhost:5123
```

## Testing

### Test Email Service Alone
```csharp
await _emailService.SendInvitationCompletionEmailAsync(
    "test@example.com",
    "abc123token",
    "Test Tenant",
    "http://localhost:5123/api/invitations/invite/abc123token"
);
// Check inbox for email
```

### Test Full Invitation Flow
```bash
# 1. Create invitation
POST /api/tenant-invitations
# 2. Check inbox - should receive 2 emails
# 3. Click password setup link ? set password
# 4. Click invitation link ? auto-login ? access granted
# 5. Verify in database: SELECT * FROM UserTenants;
```

## Troubleshooting

### Email 1 not received
- Check Keycloak realm SMTP configuration
- Verify Keycloak logs for email errors
- Check spam folder

### Email 2 not received
- Check SMTP environment variables
- Verify `EmailService` is registered in DI
- Check application logs for email errors
- Verify `SendInvitationCompletionEmailAsync` is called
- Check spam folder

### User stuck after password setup
- Verify Email 2 was sent (check logs)
- Manually provide invitation link as workaround
- Check invitation hasn't expired

### OAuth error after clicking invitation link
- Verify invitation token is valid
- Check Keycloak client configuration
- Ensure redirect URI is whitelisted
- Check OAuth callback endpoint logs

## Key Points

1. ? **Two emails are normal** - Not a bug, it's the solution
2. ? **Keycloak limitation** - Execute-actions doesn't auto-redirect
3. ? **Simple solution** - Application sends second email
4. ? **90 minutes** - Estimated implementation time
5. ? **No Keycloak customization** - Works with default setup

## Related Documentation

- **Full Analysis:** `docs/EXECUTE-ACTIONS-REDIRECT-ISSUE.md`
- **Implementation Guide:** `docs/INVITATION-EMAIL-SERVICE-IMPLEMENTATION.md`
- **Solution Summary:** `docs/INVITATION-FLOW-SOLUTION-SUMMARY.md`
- **This Reference:** `docs/INVITATION-QUICK-REFERENCE.md`

## Questions?

**Q: Why can't Keycloak redirect automatically?**
A: The `redirect_uri` parameter in execute-actions-email is stored in the token but not used for automatic redirects. It's a Keycloak design limitation.

**Q: Can we customize the Keycloak email template to include our link?**
A: Yes, but it requires creating a custom theme, mounting it into the Keycloak container, and maintaining it. The two-email approach is simpler.

**Q: What if SMTP isn't configured?**
A: The `EmailService` will log warnings but won't throw errors. Admins can manually send invitation links as a workaround.

**Q: Can we automatically accept invitations without the second email?**
A: Not securely. Users need to explicitly click a link to initiate the OAuth flow. There's no way to auto-trigger OAuth from the password setup page.

**Q: How long does implementation take?**
A: About 90 minutes for a basic implementation, including testing.

---

**Next Step:** Read `docs/INVITATION-EMAIL-SERVICE-IMPLEMENTATION.md` for detailed implementation instructions.
