# ?? Email Verification Configuration for Enterprise Realms

## Overview

When creating enterprise tenants, the system now intelligently handles email verification based on SMTP configuration from **environment variables**.

---

## ?? How It Works

### SMTP Configuration Source

When a new enterprise realm is created:

1. **Read Environment Variables**: System reads SMTP settings from `.env` file
2. **Build SMTP Config**: If all required settings are present, SMTP is configured
3. **Enable/Disable Verification**: 
   - ? **SMTP Configured**: Email verification is enabled (`verifyEmail: true`)
   - ? **No SMTP**: Email verification is disabled (`verifyEmail: false`)

### Why Not Copy from Master Realm?

**Security Note**: Keycloak Admin API does **not** return SMTP passwords for security reasons. The password field is masked when queried via API. Therefore, we use environment variables instead.

### Code Changes

**File**: `GroundUp.infrastructure/services/IdentityProviderAdminService.cs`

```csharp
// Build SMTP configuration from environment variables
var smtpServer = BuildSmtpConfiguration();

var payload = new
{
    // ...other settings...
    
    // Disable email verification if no SMTP configured
    verifyEmail = smtpServer != null && dto.VerifyEmail,
    
    // SMTP settings (from environment variables)
    smtpServer = smtpServer
};
```

---

## ?? Testing Without Email

### Current Behavior (No SMTP Configured)

1. User registers at Keycloak registration page
2. **No email verification required**
3. User is immediately logged in
4. Registration completes successfully

### What Users See

- ? Standard registration form
- ? No "Check your email" message
- ? Immediate redirect to callback
- ? Account is active immediately

---

## ?? Production Setup

### Step 1: Configure SMTP in Environment Variables

Add these settings to your `.env` file:

```sh
# SMTP Configuration for Keycloak Realms
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_FROM=noreply@yourcompany.com
SMTP_FROM_DISPLAY_NAME=Your Company Name
SMTP_REPLY_TO=support@yourcompany.com
SMTP_ENVELOPE_FROM=
SMTP_AUTH_ENABLED=true
SMTP_STARTTLS_ENABLED=true
SMTP_SSL_ENABLED=false
SMTP_USERNAME=YOUR_SMTP_USERNAME
SMTP_PASSWORD=YOUR_SMTP_PASSWORD
```

### Step 2: SMTP Provider Examples

#### AWS SES

```sh
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_FROM=noreply@yourdomain.com
SMTP_FROM_DISPLAY_NAME=Your Company
SMTP_AUTH_ENABLED=true
SMTP_STARTTLS_ENABLED=true
SMTP_SSL_ENABLED=false
SMTP_USERNAME=YOUR_SES_SMTP_USERNAME
SMTP_PASSWORD=YOUR_SES_SMTP_PASSWORD
```

**Setup Instructions**:
1. Create SES SMTP credentials in AWS Console
2. Verify your sender email/domain
3. Request production access (if needed)
4. Use SMTP credentials in `.env`

#### Gmail

```sh
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_FROM=your-email@gmail.com
SMTP_FROM_DISPLAY_NAME=Your Name
SMTP_AUTH_ENABLED=true
SMTP_STARTTLS_ENABLED=true
SMTP_SSL_ENABLED=false
SMTP_USERNAME=your-email@gmail.com
SMTP_PASSWORD=YOUR_APP_PASSWORD
```

**Setup Instructions**:
1. Enable 2-Factor Authentication on Gmail
2. Generate App Password: https://myaccount.google.com/apppasswords
3. Use the app password (not your Gmail password)

#### SendGrid

```sh
SMTP_HOST=smtp.sendgrid.net
SMTP_PORT=587
SMTP_FROM=noreply@yourdomain.com
SMTP_FROM_DISPLAY_NAME=Your Company
SMTP_AUTH_ENABLED=true
SMTP_STARTTLS_ENABLED=true
SMTP_SSL_ENABLED=false
SMTP_USERNAME=apikey
SMTP_PASSWORD=YOUR_SENDGRID_API_KEY
```

**Setup Instructions**:
1. Create SendGrid account
2. Create API key with Mail Send permission
3. Verify sender email/domain
4. Username is always `apikey`, password is your API key

### Step 3: Restart API

After updating `.env`, restart your API:

```bash
# If running via Docker
docker-compose restart api

# If running locally
# Stop and restart dotnet run
```

### Step 4: Test Enterprise Tenant Creation

1. Create a new enterprise tenant via API
2. **SMTP settings are automatically configured from environment variables**
3. **Email verification is automatically enabled**
4. New users will receive verification emails

---

## ?? Security Considerations

### Testing Environment

- ? Email verification disabled is **acceptable for testing**
- ? Reduces friction during development
- ? Allows testing without email infrastructure

### Production Environment

- ?? **Email verification should be enabled**
- ?? Prevents fake/spam account creation
- ?? Ensures user owns the email address
- ?? Required for password reset functionality
- ?? **Store SMTP password in secure secret management** (not `.env` for production)

### Production Secret Management

**Option 1: Environment Variables (Current)**
- ? Simple for development
- ? Not ideal for production (secrets in plain text)

**Option 2: Keycloak Vault (Recommended)**
- ? Secure secret storage
- ? Supports Kubernetes secrets, HashiCorp Vault
- ? Use `${vault.smtp-password}` in SMTP config
- See [Keycloak Vault Documentation](https://www.keycloak.org/server/vault)

**Option 3: Docker Secrets / Kubernetes Secrets**
- ? Mount secrets as environment variables
- ? Encrypted at rest
- ? Access control via RBAC

---

## ?? Configuration Checklist

### For Testing

- [ ] Leave SMTP settings empty in `.env` (or comment them out)
- [ ] Email verification automatically disabled for new realms
- [ ] Users can register without email confirmation
- [ ] Test the registration flow end-to-end

### For Production

- [ ] Configure SMTP settings in `.env`
- [ ] Test email sending (create test tenant)
- [ ] Verify emails are being received
- [ ] Customize email templates in Keycloak (optional)
- [ ] Configure "From" address with your domain
- [ ] Set up SPF/DKIM records for email authentication
- [ ] Move SMTP password to secure secret management

---

## ?? Troubleshooting

### Issue: Email verification disabled even with SMTP configured

**Solution**: Check that all required SMTP environment variables are set:
```bash
# Required variables:
SMTP_HOST
SMTP_USERNAME
SMTP_PASSWORD

# If any are missing, SMTP will be disabled
```

### Issue: Emails not being received

**Solution**: Check SMTP settings and logs:
1. Verify SMTP credentials are correct
2. Check Keycloak server logs for SMTP errors
3. Test SMTP connection manually
4. Verify sender email is authorized (AWS SES, SendGrid)
5. Check recipient's spam folder

### Issue: "Authentication failed" error

**Solutions**:
- **Gmail**: Make sure you're using App Password, not regular password
- **AWS SES**: Verify credentials are for SMTP (not API)
- **SendGrid**: Username should be `apikey`, not your email

### Issue: Want to test email flow without SMTP

**Solution**: Use a test email service:
- **Mailtrap**: https://mailtrap.io (free tier available)
- **MailHog**: Local SMTP server for testing
- **Ethereal**: https://ethereal.email (temporary email testing)

---

## ?? Related Documentation

- [Manual Testing Guide](./MANUAL-TESTING-GUIDE.md)
- [Enterprise Tenant Provisioning](./PHASE5-IMPLEMENTATION-COMPLETE.md)
- [AWS SES Setup](./AWS-SES-SETUP.md)
- [Email Quick Start](./QUICK-START-EMAIL.md)

---

**Status:** ? **IMPLEMENTED**  
**Last Updated:** 2025-12-03  
**Affects:** Enterprise tenant creation, user registration flow  
**Configuration Method:** Environment variables (`.env` file)
