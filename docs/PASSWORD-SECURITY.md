# User Password Security - Implementation Guide

## Overview
This document explains the password security strategy implemented for user creation in the GroundUp application.

## Current Implementation: Auto-Generated Temporary Passwords

### How It Works
When an admin creates a new user through the API:

1. **No Password Required**: The `CreateUserDto` does not include a password field
2. **Server-Side Generation**: A cryptographically secure random password is auto-generated on the server
3. **Temporary Flag**: The password is marked as temporary in Keycloak
4. **Required Action**: User is flagged with `UPDATE_PASSWORD` required action
5. **Email Notification**: User receives an email with a password reset link (if `SendWelcomeEmail` is true)
6. **First Login**: User must change their password before accessing the system

### Security Benefits

? **No Password Transmission**: Passwords are never transmitted from client to server  
? **No Password Exposure**: Admins never see or know user passwords  
? **No Logging Risk**: No passwords in logs, Swagger UI, or browser history  
? **User Control**: Users set their own passwords securely  
? **Compliance**: Follows security best practices and compliance requirements  
? **Audit Trail**: Clear separation between user creation and password setting  

### Example API Request

```json
POST /api/users
{
  "username": "john.doe",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "enabled": true,
  "sendWelcomeEmail": true
}
```

### User Experience Flow

1. **Admin creates user** ? User account created in Keycloak with temporary password
2. **User receives email** ? "Set Up Your Password" email with reset link
3. **User clicks link** ? Redirected to Keycloak password reset page
4. **User sets password** ? Enters and confirms their chosen password
5. **User logs in** ? Can now access the application with their own password

## Alternative Approaches (Not Recommended)

### ? Option 1: Accept Password in API (Original Approach)
**Why NOT Recommended:**
- Passwords transmitted over network (even with HTTPS, still exposed in transit)
- Password visible in Swagger UI, browser dev tools, logs
- Admin knows user's password (security risk)
- Password may be stored in browser history
- Violates principle of least privilege

### ? Option 2: Send Password in Encrypted Form
**Why NOT Recommended:**
- Adds complexity without significant benefit
- Client-side encryption keys must be managed
- Still requires password entry on client side
- Doesn't prevent password exposure in client memory

### ? Option 3: Current Implementation (Recommended)
See above for details.

## Configuration Options

### KeycloakConfiguration
```json
{
  "Keycloak": {
    "DefaultUserRole": "USER"  // Role automatically assigned to new users
  }
}
```

### CreateUserDto Properties
```csharp
public class CreateUserDto
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool Enabled { get; set; } = true;
    public bool EmailVerified { get; set; } = false;
    public bool SendWelcomeEmail { get; set; } = true;  // Send password setup email
    public Dictionary<string, List<string>>? Attributes { get; set; }
}
```

## Email Configuration

### Choosing Your Email Setup
Not sure which email service to use? See **[EMAIL-DECISION-GUIDE.md](./EMAIL-DECISION-GUIDE.md)** to choose between:
- AWS SES for both dev and prod (recommended for simplicity)
- MailHog for dev, AWS SES for prod (recommended for ease of testing)
- Other options (SendGrid, Mailgun, etc.)

### Development Environment
For local development:
- **MailHog**: See [EMAIL-SETUP-DEV.md](./EMAIL-SETUP-DEV.md)
- **AWS SES**: See [AWS-SES-SETUP.md](./AWS-SES-SETUP.md)

### Production - Keycloak Email Settings
For password reset emails to work in production, Keycloak must be configured with SMTP settings.

See **[EMAIL-SETUP-PRODUCTION.md](./EMAIL-SETUP-PRODUCTION.md)** for complete production setup guide.

### Email Templates
Keycloak uses customizable email templates:
- `executeActions.ftl` - Password reset action email
- Can be customized per realm in Keycloak themes

## Testing Password Flow

### Manual Testing Steps
1. Create user via API (POST /api/users)
2. Check user's email for password reset link
3. Click link and set password
4. Login with new credentials
5. Verify access to application

### Automated Testing Considerations
- Use Keycloak's Admin API to verify `requiredActions` includes `UPDATE_PASSWORD`
- Mock email service in tests to verify email sending
- Test with `SendWelcomeEmail = false` for scenarios where email isn't needed

## Troubleshooting

### User doesn't receive email
- Check Keycloak SMTP configuration
- Verify email address is valid
- Check spam/junk folders
- Review Keycloak server logs for email errors
- Test SMTP connection from Keycloak admin console

### Password reset link expired
- Default expiration is typically 5 minutes
- Can be configured in Keycloak: Realm Settings ? Tokens ? Action Token Lifespan
- Resend email using: POST /api/users/{userId}/reset-password

### User unable to set password
- Verify `UPDATE_PASSWORD` is in user's required actions
- Check password policy in Keycloak matches user's chosen password
- Review Keycloak password policy: Authentication ? Policies ? Password Policy

## Security Considerations

### Password Generation
- Uses cryptographically secure random generation
- 16 characters minimum
- Includes uppercase, lowercase, digits, and special characters
- Shuffled to avoid predictable patterns

### Temporary Password Storage
- Temporary password only exists in Keycloak
- Never logged or persisted in application database
- Automatically invalidated after user sets new password

### Password Reset Token
- Time-limited (configurable in Keycloak)
- Single-use token
- Delivered via secure email link
- Includes verification of user's identity

## Compliance & Best Practices

### OWASP Recommendations
? Passwords are not transmitted from client  
? Passwords are not logged  
? Users set their own passwords  
? Password complexity requirements enforced  
? Secure password storage (bcrypt in Keycloak)  

### GDPR/Privacy
? Minimal data collection (no unnecessary password exposure)  
? User consent through email verification  
? Clear audit trail of who created the account  

### SOC 2 / ISO 27001
? Principle of least privilege (admin can't know passwords)  
? Segregation of duties (account creation vs password setting)  
? Audit logging enabled  

## Migration from Previous Implementation

If you previously had password in `CreateUserDto`:

### Code Changes Needed
1. ? Remove `Password` property from `CreateUserDto`
2. ? Add `SendWelcomeEmail` property (defaults to true)
3. ? Update validators to remove password validation
4. ? Update `IdentityProviderAdminService.CreateUserAsync` to generate password
5. ? Update API documentation/Swagger examples

### Breaking Changes
- API consumers must update their requests to **not** include password field
- Existing integration tests may need updating
- API documentation must be updated

### Migration Script Example
```csharp
// OLD
var createUserDto = new CreateUserDto
{
    Username = "john.doe",
    Email = "john.doe@example.com",
    Password = "UserPassword123!",  // REMOVED
    FirstName = "John",
    LastName = "Doe"
};

// NEW
var createUserDto = new CreateUserDto
{
    Username = "john.doe",
    Email = "john.doe@example.com",
    // Password removed - auto-generated
    FirstName = "John",
    LastName = "Doe",
    SendWelcomeEmail = true  // NEW - Optional, defaults to true
};
```

## Future Enhancements

### Potential Improvements
- [ ] Allow configuration of password generation length/complexity
- [ ] Support for different welcome email templates per tenant
- [ ] Add SMS-based password reset as alternative to email
- [ ] Implement password reset link expiration configuration per user
- [ ] Add webhook for password change events
- [ ] Support for social login providers (Google, Microsoft, etc.)

---

**Last Updated**: {Current Date}  
**Version**: 1.0  
**Related Documents**: 
- [SECURITY-CHECKLIST.md](./SECURITY-CHECKLIST.md)
- [Authentication-Wiki.md](./Authentication-Wiki.md)
