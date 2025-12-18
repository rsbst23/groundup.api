# Email Integration Roadmap

## Current Status

The **Tenant Invitation System** is fully functional without email integration.

### What Works Now
- ? Admins can create invitations via API
- ? Invitation tokens are generated and stored
- ? Users can accept invitations via API
- ? User-tenant assignments work correctly
- ? Multi-tenant support implemented
- ? Invitation validation (expiration, email matching)

### What's Missing
- ? Automatic email delivery of invitation links
- ? Email template customization
- ? Email provider configuration

---

## Why Email Was Deferred

Email integration requires a **comprehensive settings infrastructure** that goes beyond just sending emails:

1. **Settings/Configuration System** - Generic key-value store for app configuration
2. **Template Management** - HTML/text email templates with variable substitution
3. **Email Service Abstraction** - Support for SMTP, SendGrid, AWS SES, etc.
4. **Frontend Settings UI** - Targeted screens for email, user, AI settings
5. **Per-Tenant Configuration** - Multi-tenant support for all settings

Building email properly means building **5 major systems**. The invitation feature is complete and testable without email.

---

## Workaround: Manual Token Distribution

Until email is implemented, invitations work with manual token distribution:

### Admin Workflow
```http
POST /api/invitations
{
  "email": "newuser@example.com",
  "isAdmin": false,
  "expirationDays": 7
}

Response:
{
  "data": {
    "id": 123,
    "email": "newuser@example.com",
    "invitationToken": "abc123def456...",
    "expiresAt": "2025-11-27T00:00:00Z"
  }
}
```

**Admin then:**
- Copies `invitationToken` from response
- Sends to user via Slack, Teams, email (manually), etc.
- User receives link: `https://app.example.com/accept-invite?token=abc123def456...`

### User Workflow
```http
POST /api/invitations/accept
{
  "invitationToken": "abc123def456..."
}

Response:
{
  "data": true,
  "message": "Invitation accepted successfully. You now have access to the tenant."
}
```

---

## Future Implementation Plan

### Phase 1: Settings Infrastructure (Foundation)

**Goal:** Generic, multi-tenant settings system

**Entities:**
```csharp
public class AppSetting : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Category { get; set; }  // "Email", "AI", "User", "Notification"
    public string Key { get; set; }       // "SmtpHost", "ApiKey", "Provider"
    public string Value { get; set; }     // Encrypted for sensitive data
    public bool IsEncrypted { get; set; }
    public SettingType Type { get; set; } // String, Int, Bool, Json
    public string? Description { get; set; }
}

public class Template : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Category { get; set; }  // "Email", "SMS", "Notification"
    public string Name { get; set; }      // "InvitationEmail", "PasswordReset"
    public string Subject { get; set; }   // Email subject line
    public string HtmlBody { get; set; }  // HTML template
    public string TextBody { get; set; }  // Plain text fallback
    public string Variables { get; set; } // JSON array of available variables
}
```

**API Endpoints:**
```http
# Settings CRUD
GET    /api/settings?category=Email           # Get email settings
GET    /api/settings/{category}/{key}         # Get specific setting
POST   /api/settings                          # Create setting
PUT    /api/settings/{id}                     # Update setting
DELETE /api/settings/{id}                     # Delete setting

# Templates CRUD
GET    /api/templates?category=Email          # Get email templates
GET    /api/templates/{id}                    # Get specific template
POST   /api/templates                         # Create template
PUT    /api/templates/{id}                    # Update template
DELETE /api/templates/{id}                    # Delete template
POST   /api/templates/{id}/preview            # Preview with sample data
```

**Repositories:**
- `IAppSettingRepository` / `AppSettingRepository`
- `ITemplateRepository` / `TemplateRepository`

### Phase 2: Email Service Layer

**Goal:** Provider-agnostic email sending

**Interface:**
```csharp
public interface IEmailService
{
    // Send using a template
    Task<EmailResult> SendTemplatedEmailAsync(
        string templateName, 
        string toEmail, 
        Dictionary<string, string> variables);
    
    // Send raw email
    Task<EmailResult> SendEmailAsync(
        string to, 
        string subject, 
        string htmlBody, 
        string? textBody = null);
    
    // Test email configuration
    Task<bool> TestConfigurationAsync();
}

public class EmailResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }
}
```

**Implementations:**
```csharp
public class SmtpEmailService : IEmailService
{
    // Uses System.Net.Mail.SmtpClient
}

public class SendGridEmailService : IEmailService
{
    // Uses SendGrid SDK
}

public class AwsSesEmailService : IEmailService
{
    // Uses AWS SDK
}

// Factory pattern to select provider
public class EmailServiceFactory
{
    public IEmailService Create(string provider)
    {
        // Read from AppSettings to determine provider
        // Return appropriate implementation
    }
}
```

**Configuration (in AppSettings):**
```json
{
  "category": "Email",
  "settings": [
    { "key": "Provider", "value": "SMTP" },
    { "key": "SmtpHost", "value": "smtp.gmail.com" },
    { "key": "SmtpPort", "value": "587" },
    { "key": "SmtpUsername", "value": "user@example.com", "isEncrypted": true },
    { "key": "SmtpPassword", "value": "encrypted...", "isEncrypted": true },
    { "key": "FromEmail", "value": "noreply@example.com" },
    { "key": "FromName", "value": "GroundUp" }
  ]
}
```

### Phase 3: Email Template System

**Goal:** Customizable email templates with variable substitution

**Default Templates to Create:**

#### 1. Invitation Email
```html
<!-- Template Name: InvitationEmail -->
<!-- Variables: {{InvitationToken}}, {{TenantName}}, {{InviterName}}, {{ExpirationDate}} -->

<!DOCTYPE html>
<html>
<head>
    <style>
        .email-container { max-width: 600px; margin: 0 auto; font-family: Arial, sans-serif; }
        .button { background-color: #4CAF50; color: white; padding: 14px 20px; text-decoration: none; }
    </style>
</head>
<body>
    <div class="email-container">
        <h2>You've been invited to join {{TenantName}}</h2>
        <p>{{InviterName}} has invited you to join their organization on GroundUp.</p>
        
        <p>Click the button below to accept this invitation:</p>
        
        <a href="{{AcceptInvitationUrl}}" class="button">Accept Invitation</a>
        
        <p>Or copy and paste this link into your browser:</p>
        <p>{{AcceptInvitationUrl}}</p>
        
        <p><small>This invitation expires on {{ExpirationDate}}.</small></p>
    </div>
</body>
</html>
```

#### 2. Password Reset Email
```html
<!-- Template Name: PasswordResetEmail -->
<!-- Variables: {{ResetToken}}, {{UserName}}, {{ExpirationMinutes}} -->
```

#### 3. Welcome Email
```html
<!-- Template Name: WelcomeEmail -->
<!-- Variables: {{UserName}}, {{TenantName}} -->
```

**Template Variable Substitution:**
```csharp
public class TemplateEngine
{
    public string Render(string template, Dictionary<string, string> variables)
    {
        var result = template;
        foreach (var variable in variables)
        {
            result = result.Replace($"{{{{{variable.Key}}}}}", variable.Value);
        }
        return result;
    }
}
```

### Phase 4: Integration with Invitation System

**Update `TenantInvitationRepository.AddAsync`:**
```csharp
public async Task<ApiResponse<TenantInvitationDto>> AddAsync(...)
{
    // ... existing code to create invitation ...
    
    // Send email (non-blocking - log failures but don't fail creation)
    try
    {
        var emailService = _emailServiceFactory.Create();
        var acceptUrl = $"{_appSettings.BaseUrl}/accept-invite?token={invitation.InvitationToken}";
        
        await emailService.SendTemplatedEmailAsync(
            "InvitationEmail",
            invitation.Email,
            new Dictionary<string, string>
            {
                { "InvitationToken", invitation.InvitationToken },
                { "TenantName", tenant.Name },
                { "InviterName", createdByUser.Username },
                { "ExpirationDate", invitation.ExpiresAt.ToString("MMMM dd, yyyy") },
                { "AcceptInvitationUrl", acceptUrl }
            }
        );
        
        _logger.LogInformation($"Invitation email sent to {invitation.Email}");
    }
    catch (Exception ex)
    {
        _logger.LogError($"Failed to send invitation email: {ex.Message}", ex);
        // Don't fail invitation creation if email fails
    }
    
    return new ApiResponse<TenantInvitationDto>(invitationDto, true, 
        "Invitation created successfully");
}
```

**Update `ResendInvitationAsync`:** Same pattern - send email after updating expiration.

### Phase 5: Frontend Settings UI

**Goal:** Targeted settings screens (not generic dumps)

**Email Settings Screen** (`/settings/email`)
```typescript
// Email Provider Configuration
- Provider Selection (SMTP, SendGrid, AWS SES)
- Connection Details (host, port, credentials)
- Test Connection Button
- From Address Configuration

// Email Templates
- List of templates (Invitation, Password Reset, Welcome)
- Template Editor (HTML + Text)
- Variable Helper (shows available {{variables}})
- Preview with Sample Data
- Test Send Button
```

**User Settings Screen** (`/settings/users`)
```typescript
// User Onboarding
- Default role for new users
- Require email verification
- Welcome email enabled/disabled
- Default permissions
```

**AI Settings Screen** (`/settings/ai`) *(Future)*
```typescript
// AI Integration
- Provider (OpenAI, Anthropic, etc.)
- API Key
- Model Selection
- Temperature/Parameters
```

**Generic Settings Component** (Reusable)
```typescript
// Generic key-value settings component
<SettingsEditor 
  category="Email" 
  settings={emailSettings}
  onSave={handleSave}
/>
```

---

## Testing Strategy

### Phase 1-2: Settings & Email Service
- ? Unit tests for settings CRUD
- ? Unit tests for template rendering
- ? Integration tests for email sending
- ? Mock email service for testing

### Phase 3: Template System
- ? Test variable substitution
- ? Test HTML/text rendering
- ? Test missing variable handling

### Phase 4: Invitation Integration
- ? Test invitation email sending
- ? Test email failures don't break invitations
- ? Test resend invitation email
- ? Test invitation links are correct

### Phase 5: Frontend
- ? Test settings CRUD operations
- ? Test template editor
- ? Test preview functionality
- ? Test connection testing

---

## Security Considerations

### Encrypted Settings
```csharp
public class EncryptionService
{
    public string Encrypt(string value);
    public string Decrypt(string encryptedValue);
}

// Use for SMTP passwords, API keys, etc.
```

### Email Rate Limiting
```csharp
// Prevent email bombing
public class EmailRateLimiter
{
    Task<bool> CanSendEmail(string toEmail);
}
```

### Template Security
- ? No script execution in templates
- ? HTML sanitization
- ? Variable whitelist only

---

## Migration Path

### Current State ? Settings Infrastructure
1. Create `AppSetting` and `Template` entities
2. Create migrations
3. Create repositories
4. Create settings API endpoints
5. Test CRUD operations

### Settings Infrastructure ? Email Service
1. Implement `IEmailService` interface
2. Implement provider-specific services
3. Create email service factory
4. Add email configuration UI
5. Test email sending

### Email Service ? Template System
1. Create default email templates
2. Implement template rendering
3. Add template management UI
4. Test template preview

### Template System ? Invitation Integration
1. Update `TenantInvitationRepository`
2. Add email sending to create/resend
3. Make email failures non-blocking
4. Test full invitation flow

---

## Estimated Effort

| Phase | Effort | Priority |
|-------|--------|----------|
| Phase 1: Settings Infrastructure | 2-3 weeks | High (Foundation for everything) |
| Phase 2: Email Service | 1-2 weeks | High (Enables email) |
| Phase 3: Template System | 1 week | Medium (Nice to have) |
| Phase 4: Invitation Integration | 2-3 days | High (Completes invitation flow) |
| Phase 5: Frontend UI | 2-3 weeks | Medium (UX improvement) |

**Total: ~6-8 weeks** for complete email + settings infrastructure

---

## Alternative: Quick Email Integration (Not Recommended)

If you need email **immediately** without full settings infrastructure:

### Quick & Dirty Approach
```csharp
// Hardcoded email configuration (environment variables)
public class SimpleEmailService
{
    private readonly string _smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
    private readonly string _smtpUser = Environment.GetEnvironmentVariable("SMTP_USER");
    // ... etc
    
    public async Task SendInvitationEmail(string to, string token, string tenantName)
    {
        // Hardcoded HTML template
        var html = $@"
            <h2>You've been invited to {tenantName}</h2>
            <a href='https://app.example.com/accept?token={token}'>Accept Invitation</a>
        ";
        
        // Send using SmtpClient
    }
}
```

**Why This Is Bad:**
- ? No template customization
- ? No provider flexibility
- ? Hardcoded configuration
- ? Technical debt
- ? Will need to be rewritten later

**Only do this if:** You need email **this week** for a demo/MVP and will replace it soon.

---

## Conclusion

**Current Recommendation: Defer Email**

The invitation system is **complete and functional** without email. Adding email properly requires building a comprehensive settings/configuration infrastructure that will benefit the entire application.

**When to build email:**
- When you're ready to tackle application-wide settings
- When you need template customization
- When you're building the frontend settings screens

**For now:**
- ? Test invitation system manually
- ? Document the email gap (this file)
- ? Move forward with other features
- ? Come back to email when you build the settings infrastructure

---

## References

- `docs/EMAIL-SETUP-DEV.md` - Development email setup
- `docs/EMAIL-SETUP-PRODUCTION.md` - Production email setup
- `docs/AWS-SES-SETUP.md` - AWS SES configuration
- `docs/EMAIL-DECISION-GUIDE.md` - Email provider comparison
- `docs/CONTINUE-INVITATION-SYSTEM.md` - Invitation system status

---

Last Updated: 2025-11-20
Status: Planning Document
Next Steps: Test invitation system without email, defer email to Phase 1
