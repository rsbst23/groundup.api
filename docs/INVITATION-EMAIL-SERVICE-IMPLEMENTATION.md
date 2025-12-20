# Invitation Follow-Up Email Service Implementation

## Overview

This document provides step-by-step instructions for implementing the follow-up invitation email that users receive after setting their password via Keycloak's execute-actions email.

## Problem We're Solving

Keycloak's `execute-actions-email` doesn't automatically redirect users after they complete required actions. Users set their password and then are stuck on an "Account updated" page with no clear next step.

**Solution:** Send a second email with the invitation completion link: `/api/invitations/invite/{token}`

## Implementation Steps

### Step 1: Create IEmailService Interface

**File:** `GroundUp.core/interfaces/IEmailService.cs`

```csharp
namespace GroundUp.core.interfaces
{
    /// <summary>
    /// Service for sending application emails via SMTP
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Sends an HTML email to a single recipient
        /// </summary>
        /// <param name="to">Recipient email address</param>
        /// <param name="subject">Email subject line</param>
        /// <param name="htmlBody">HTML email body</param>
        /// <returns>True if email sent successfully</returns>
        Task<bool> SendEmailAsync(string to, string subject, string htmlBody);
        
        /// <summary>
        /// Sends invitation completion email with link to accept invitation
        /// </summary>
        /// <param name="email">Recipient email address</param>
        /// <param name="invitationToken">The invitation token</param>
        /// <param name="tenantName">Name of the tenant</param>
        /// <param name="invitationUrl">Full URL to invitation acceptance endpoint</param>
        /// <returns>True if email sent successfully</returns>
        Task<bool> SendInvitationCompletionEmailAsync(
            string email,
            string invitationToken,
            string tenantName,
            string invitationUrl);
    }
}
```

### Step 2: Implement EmailService

**File:** `GroundUp.infrastructure/services/EmailService.cs`

```csharp
using GroundUp.core.interfaces;
using System.Net;
using System.Net.Mail;

namespace GroundUp.infrastructure.services
{
    public class EmailService : IEmailService
    {
        private readonly ILoggingService _logger;
        private readonly string? _smtpHost;
        private readonly int _smtpPort;
        private readonly string? _smtpUsername;
        private readonly string? _smtpPassword;
        private readonly string? _smtpFrom;
        private readonly string? _smtpFromDisplayName;
        private readonly bool _smtpEnabled;

        public EmailService(ILoggingService logger)
        {
            _logger = logger;
            
            // Load SMTP configuration from environment
            _smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
            _smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME");
            _smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
            _smtpFrom = Environment.GetEnvironmentVariable("SMTP_FROM") ?? "noreply@groundup.com";
            _smtpFromDisplayName = Environment.GetEnvironmentVariable("SMTP_FROM_DISPLAY_NAME") ?? "GroundUp";
            
            var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
            int.TryParse(smtpPortStr, out _smtpPort);
            
            _smtpEnabled = !string.IsNullOrEmpty(_smtpHost) && 
                          !string.IsNullOrEmpty(_smtpUsername) && 
                          !string.IsNullOrEmpty(_smtpPassword);
            
            if (_smtpEnabled)
            {
                _logger.LogInformation($"EmailService initialized with SMTP host: {_smtpHost}:{_smtpPort}");
            }
            else
            {
                _logger.LogWarning("EmailService initialized but SMTP is not configured - emails will not be sent");
            }
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string htmlBody)
        {
            if (!_smtpEnabled)
            {
                _logger.LogWarning($"Cannot send email - SMTP not configured. Would send to: {to}, Subject: {subject}");
                return false;
            }

            try
            {
                using var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
                {
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpFrom, _smtpFromDisplayName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                
                mailMessage.To.Add(to);

                await smtpClient.SendMailAsync(mailMessage);
                
                _logger.LogInformation($"Email sent successfully to {to}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send email to {to}: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<bool> SendInvitationCompletionEmailAsync(
            string email,
            string invitationToken,
            string tenantName,
            string invitationUrl)
        {
            var subject = $"Complete Your Invitation to {tenantName}";
            
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{subject}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f4f4f4; padding: 20px; border-radius: 5px;"">
        <h2 style=""color: #4CAF50; margin-top: 0;"">Welcome to {tenantName}!</h2>
        
        <p>You should have received an email to set up your account password.</p>
        
        <p><strong>After setting your password</strong>, click the button below to complete your invitation and gain access to {tenantName}:</p>
        
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{invitationUrl}"" 
               style=""background-color: #4CAF50; 
                      color: white; 
                      padding: 15px 30px; 
                      text-decoration: none; 
                      border-radius: 5px; 
                      display: inline-block;
                      font-weight: bold;"">
                Complete Your Invitation
            </a>
        </div>
        
        <p>This link will securely log you in and grant you access to {tenantName}.</p>
        
        <hr style=""border: none; border-top: 1px solid #ddd; margin: 30px 0;"">
        
        <p style=""font-size: 12px; color: #666;"">
            If you didn't request this invitation, please ignore this email.<br>
            This invitation will expire in 7 days.
        </p>
    </div>
</body>
</html>";

            return await SendEmailAsync(email, subject, htmlBody);
        }
    }
}
```

### Step 3: Register Service in DI Container

**File:** `GroundUp.infrastructure/extensions/ServiceCollectionExtensions.cs`

```csharp
// Add to ConfigureInfrastructureServices method

// Email Service
services.AddSingleton<IEmailService, EmailService>();
```

### Step 4: Update TenantInvitationRepository

**File:** `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs`

```csharp
// Add field
private readonly IEmailService _emailService;

// Update constructor
public TenantInvitationRepository(
    ApplicationDbContext context,
    IMapper mapper,
    ILoggingService logger,
    ITenantContext tenantContext,
    IUserTenantRepository userTenantRepo,
    IIdentityProviderAdminService identityProvider,
    IEmailService emailService)  // Add this parameter
    : base(context, mapper, logger, tenantContext)
{
    _userTenantRepo = userTenantRepo;
    _identityProvider = identityProvider;
    _emailService = emailService;  // Add this
}

// Update AddAsync method - after sending execute-actions email
if (!emailSent)
{
    _logger.LogWarning($"Failed to send execute actions email for invitation {invitation.Id}");
}
else
{
    _logger.LogInformation($"Successfully sent execute actions email for invitation {invitation.Id}");
    
    // Send follow-up invitation completion email
    var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5123";
    var invitationUrl = $"{apiUrl}/api/invitations/invite/{invitation.InvitationToken}";
    
    var followUpSent = await _emailService.SendInvitationCompletionEmailAsync(
        dto.Email,
        invitation.InvitationToken,
        tenant.Name,
        invitationUrl
    );
    
    if (followUpSent)
    {
        _logger.LogInformation($"Successfully sent invitation completion email for invitation {invitation.Id}");
    }
    else
    {
        _logger.LogWarning($"Failed to send invitation completion email for invitation {invitation.Id}");
    }
}
```

### Step 5: Update Environment Variables

**File:** `.env` or `GroundUp.api/.env`

```bash
# SMTP Configuration (AWS SES)
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_USERNAME=your_aws_ses_smtp_username
SMTP_PASSWORD=your_aws_ses_smtp_password
SMTP_FROM=noreply@yourdomain.com
SMTP_FROM_DISPLAY_NAME=GroundUp

# API Configuration
API_URL=http://localhost:5123
```

### Step 6: Test the Flow

1. **Create invitation:**
```bash
POST http://localhost:5123/api/tenant-invitations
Content-Type: application/json

{
  "email": "test@example.com",
  "isAdmin": false,
  "expirationDays": 7
}
```

2. **Check email inbox:**
   - First email: Keycloak execute-actions (password setup)
   - Second email: Invitation completion email

3. **User flow:**
   - Click first email ? Set password ? See "Account updated"
   - Click second email ? Redirected to OAuth ? Auto-login ? Invitation accepted

4. **Verify in database:**
```sql
SELECT * FROM UserTenants WHERE UserId = '{user-id}';
-- Should show tenant assignment
```

## Testing Without SMTP

If SMTP is not configured, the service will log warnings but won't fail:

```csharp
_logger.LogWarning("Cannot send email - SMTP not configured. Would send to: {email}");
```

This allows development without AWS SES configured.

## Production Considerations

1. **Email Rate Limits:** AWS SES has sending limits - check your quotas
2. **Email Deliverability:** Verify domain in AWS SES to avoid spam folder
3. **Error Handling:** Consider retry logic for transient email failures
4. **Logging:** Log all email sends for audit trail
5. **Templates:** Consider moving HTML templates to separate files for easier editing

## Future Enhancements

1. **Email Templates:** Move HTML to template files with variable substitution
2. **Email Queue:** Add background job queue for email sending
3. **Retry Logic:** Implement exponential backoff for failed sends
4. **Custom Branding:** Allow tenants to customize email appearance
5. **Email Tracking:** Track email opens and link clicks

## Alternative: Custom Keycloak Email Template

For a single-email solution, customize Keycloak's email template:

1. Create custom theme in Keycloak
2. Override `email/html/executeActions.ftl`
3. Add invitation link to template
4. Mount custom theme into Keycloak container

See [Keycloak Email Customization Guide](https://www.keycloak.org/docs/latest/server_development/#_email-templates) for details.

## Success Criteria

- ? User receives password setup email from Keycloak
- ? User receives invitation completion email from application
- ? User can set password without issues
- ? User clicks invitation link and is automatically logged in
- ? Invitation is accepted and user gains tenant access
- ? No manual steps required (fully automated)

## Troubleshooting

### Email Not Received

1. Check SMTP credentials are correct
2. Verify AWS SES domain verification
3. Check spam/junk folder
4. Review application logs for SMTP errors
5. Test SMTP connection with telnet: `telnet email-smtp.us-east-1.amazonaws.com 587`

### Email Template Rendering Issues

1. Check HTML validity
2. Test with multiple email clients
3. Use online email HTML testers
4. Keep CSS inline (most email clients strip `<style>` tags)

### User Still Stuck After Password Setup

1. Verify follow-up email was sent (check logs)
2. Check invitation token is valid and not expired
3. Ensure `/api/invitations/invite/{token}` endpoint is accessible
4. Verify Keycloak OAuth configuration is correct
