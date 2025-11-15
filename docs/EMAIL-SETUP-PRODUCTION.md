# Production Email Configuration Guide

## Overview
This guide explains how to configure email for **production** environments. 

**?? WARNING: Do NOT use MailHog in production!**

MailHog is a development-only tool that captures emails locally and never sends them to real recipients.

---

## Production Email Service Options

### Option 1: AWS SES (Recommended for AWS deployments)

**Why AWS SES?**
- ? Highly reliable (99.9% SLA)
- ? Very low cost ($0.10 per 1,000 emails)
- ? Easy integration with AWS services
- ? Built-in bounce/complaint handling
- ? Scalable to millions of emails

#### Setup Steps

1. **Create AWS SES Account**
   - Go to AWS Console ? SES
   - Verify your domain or email address
   - Request production access (starts in sandbox mode)

2. **Create SMTP Credentials**
   - SES ? SMTP Settings ? Create SMTP Credentials
   - Save the username and password securely

3. **Configure Keycloak**
   ```
   Host: email-smtp.us-east-1.amazonaws.com  (or your region)
   Port: 587
   From: noreply@yourdomain.com
   From Display Name: GroundUp Application
   Encryption: STARTTLS (Enable SSL)
   Authentication: Enabled
   Username: YOUR_SES_SMTP_USERNAME
   Password: YOUR_SES_SMTP_PASSWORD
   ```

4. **Configure SPF/DKIM**
   - Add DNS records to verify domain
   - Prevents emails from going to spam

5. **Monitor Delivery**
   - Set up CloudWatch alarms
   - Monitor bounce and complaint rates

**Cost Example:**
- First 62,000 emails/month: Free (if sent from EC2)
- After that: $0.10 per 1,000 emails
- Receiving: $0.10 per 1,000 emails

---

### Option 2: SendGrid

**Why SendGrid?**
- ? Easy setup
- ? Free tier (100 emails/day)
- ? Great deliverability
- ? Built-in analytics
- ? Template management

#### Setup Steps

1. **Sign Up**
   - Go to https://sendgrid.com
   - Create free account

2. **Create API Key**
   - Settings ? API Keys ? Create API Key
   - Save the key securely

3. **Configure Keycloak**
   ```
   Host: smtp.sendgrid.net
   Port: 587
   From: noreply@yourdomain.com
   From Display Name: GroundUp Application
   Encryption: STARTTLS
   Authentication: Enabled
   Username: apikey  (literally the word "apikey")
   Password: YOUR_SENDGRID_API_KEY
   ```

**Pricing:**
- Free: 100 emails/day
- Essentials: $19.95/month (50,000 emails)
- Pro: $89.95/month (100,000 emails)

---

### Option 3: Mailgun

**Why Mailgun?**
- ? Developer-friendly API
- ? Good free tier
- ? Detailed logs
- ? Easy validation

#### Setup Steps

1. **Sign Up**
   - Go to https://www.mailgun.com
   - Create account

2. **Verify Domain**
   - Add DNS records
   - Verify domain ownership

3. **Get SMTP Credentials**
   - Domain Settings ? SMTP Credentials

4. **Configure Keycloak**
   ```
   Host: smtp.mailgun.org
   Port: 587
   From: noreply@yourdomain.com
   Encryption: STARTTLS
   Authentication: Enabled
   Username: postmaster@yourdomain.com
   Password: YOUR_MAILGUN_PASSWORD
   ```

**Pricing:**
- Free: 5,000 emails/month for 3 months
- Foundation: $35/month (50,000 emails)

---

### Option 4: Azure Communication Services

**Why Azure Communication Services?**
- ? Perfect for Azure deployments
- ? Integrated with Azure services
- ? Enterprise-grade reliability
- ? Pay-as-you-go pricing

#### Setup Steps

1. **Create Resource**
   - Azure Portal ? Communication Services ? Create

2. **Get Connection String**
   - Resource ? Keys
   - Copy connection string

3. **Enable Email**
   - Communication Services ? Email ? Add domain

4. **Configure Keycloak**
   ```
   Host: smtp.azurecomm.net  (check your region)
   Port: 587
   From: noreply@yourdomain.com
   Encryption: STARTTLS
   Authentication: Enabled
   Username: <your-resource-name>
   Password: <access-key>
   ```

**Pricing:**
- $0.25 per 1,000 emails

---

### Option 5: Office 365 / Microsoft 365

**Use Case:** If your organization already uses Office 365

#### Configure Keycloak

```
Host: smtp.office365.com
Port: 587
From: noreply@yourcompany.com
Encryption: STARTTLS
Authentication: Enabled
Username: noreply@yourcompany.com
Password: YOUR_APP_PASSWORD  (not your regular password!)
```

**Important:**
- Use an **App Password**, not your regular password
- Enable Modern Authentication
- May have sending limits (typically 10,000/day)

---

### Option 6: Google Workspace

**Use Case:** If your organization uses Google Workspace

#### Configure Keycloak

```
Host: smtp.gmail.com
Port: 587
From: noreply@yourcompany.com
Encryption: STARTTLS
Authentication: Enabled
Username: noreply@yourcompany.com
Password: YOUR_APP_PASSWORD
```

**Setup App Password:**
1. Enable 2-Factor Authentication
2. Google Account ? Security ? App passwords
3. Generate app password for "Mail"

**Limits:**
- 2,000 emails per day for Google Workspace
- 500 emails per day for free Gmail

---

## Securing Production SMTP Credentials

### ? NEVER Do This

```json
// appsettings.Production.json - DON'T DO THIS!
{
  "Smtp": {
    "Password": "my-password-in-plaintext"  // ? SECURITY RISK!
  }
}
```

### ? Use Secret Management

#### Option A: Azure Key Vault

```bash
# Store secret in Key Vault
az keyvault secret set \
  --vault-name mykeyvault \
  --name smtp-password \
  --value "your-smtp-password"
```

```csharp
// Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential()
);
```

#### Option B: AWS Secrets Manager

```bash
# Store secret
aws secretsmanager create-secret \
  --name groundup/smtp/password \
  --secret-string "your-smtp-password"
```

#### Option C: Environment Variables

```bash
# Set environment variable
export KEYCLOAK_SMTP_PASSWORD="your-smtp-password"
```

```yaml
# docker-compose.production.yml
keycloak:
  environment:
    SMTP_PASSWORD: ${KEYCLOAK_SMTP_PASSWORD}
```

---

## Docker Compose for Production

### ? Development (MailHog)

```yaml
# docker-compose.dev.yml
services:
  mailhog:
    image: mailhog/mailhog:latest
    ports:
      - "1025:1025"
      - "8025:8025"
```

### ? Production (No MailHog)

```yaml
# docker-compose.production.yml
services:
  keycloak:
    environment:
      # Real SMTP settings from environment variables
      KC_SPI_EMAIL_TEMPLATE_PROVIDER: custom
      SMTP_HOST: ${SMTP_HOST}
      SMTP_PORT: ${SMTP_PORT}
      SMTP_FROM: ${SMTP_FROM}
      SMTP_USERNAME: ${SMTP_USERNAME}
      SMTP_PASSWORD: ${SMTP_PASSWORD}
  
  # No mailhog service in production!
```

---

## Keycloak Production Email Configuration

### Manual Configuration

1. **Login to Keycloak Admin Console**
2. **Navigate to:** Realm Settings ? Email
3. **Configure Production SMTP:**

```
From: noreply@yourdomain.com
From Display Name: GroundUp Application
Reply To: support@yourdomain.com
Reply To Display Name: GroundUp Support
Envelope From: (leave empty)

Host: <your-smtp-host>
Port: 587 (or 465 for SSL)
Encryption: Enable SSL/StartTLS
Authentication: Enabled
Username: <your-smtp-username>
Password: <your-smtp-password>
```

4. **Test Connection**
   - Click "Test connection"
   - Enter a real email address
   - Verify email arrives

### Automated Configuration (Terraform/Script)

You can also configure Keycloak SMTP via Admin API:

```bash
curl -X PUT "http://keycloak:8080/admin/realms/GroundUp" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "smtpServer": {
      "from": "noreply@yourdomain.com",
      "fromDisplayName": "GroundUp Application",
      "host": "smtp.sendgrid.net",
      "port": "587",
      "ssl": "true",
      "starttls": "true",
      "auth": "true",
      "user": "apikey",
      "password": "YOUR_API_KEY"
    }
  }'
```

---

## Email Deliverability Best Practices

### 1. Domain Authentication

**SPF Record:**
```
v=spf1 include:_spf.sendgrid.net ~all
```

**DKIM Record:**
- Your email provider will give you DKIM records to add to DNS

**DMARC Record:**
```
v=DMARC1; p=none; rua=mailto:dmarc@yourdomain.com
```

### 2. Email Content Best Practices

- ? Use clear, professional sender name
- ? Include plain text version (not just HTML)
- ? Provide unsubscribe link
- ? Use consistent "From" address
- ? Avoid spam trigger words
- ? Include physical address in footer

### 3. Monitoring & Alerts

**Set up monitoring for:**
- Bounce rate (should be < 5%)
- Complaint rate (should be < 0.1%)
- Delivery rate (should be > 95%)
- SMTP connection failures

**CloudWatch/Azure Monitor Alerts:**
```yaml
Alert when:
  - Bounce rate > 5%
  - SMTP authentication fails
  - Email send rate drops significantly
  - Complaint rate > 0.1%
```

---

## Testing Production Email

### Pre-Production Testing

1. **Test with Real Email Addresses**
   ```bash
   # Create test user
   curl -X POST https://your-api.com/api/users \
     -d '{"email": "your-real-email@gmail.com", ...}'
   ```

2. **Check Inbox**
   - Verify email arrives
   - Check it's not in spam
   - Click password reset link
   - Verify it works end-to-end

3. **Test Different Email Providers**
   - Gmail
   - Outlook/Hotmail
   - Yahoo
   - Corporate email servers

### Production Smoke Tests

After deployment:
```bash
# Send test email via Keycloak API
curl -X POST https://your-keycloak.com/admin/realms/GroundUp/users/{userId}/execute-actions-email \
  -H "Authorization: Bearer $TOKEN" \
  -d '["UPDATE_PASSWORD"]'
```

---

## Troubleshooting Production Email

### Email Not Delivered

**Check 1: SMTP Credentials**
- Verify username/password are correct
- Check credentials haven't expired
- Verify API key has email permissions

**Check 2: DNS Configuration**
- Verify SPF, DKIM, DMARC records
- Use tools like https://mxtoolbox.com

**Check 3: Sending Limits**
- Check if you've hit rate limits
- Verify account is in production mode (not sandbox)

**Check 4: Recipient Issues**
- Check spam folder
- Verify recipient email is valid
- Check bounce notifications

### Emails Going to Spam

**Solutions:**
1. Configure SPF/DKIM/DMARC
2. Warm up your IP address (gradually increase volume)
3. Maintain low bounce/complaint rates
4. Use consistent "From" address
5. Request email provider whitelist your domain

### SMTP Connection Errors

**Check:**
- Firewall rules allow outbound port 587/465
- Security groups allow SMTP traffic
- Network ACLs configured correctly
- Credentials are correct
- Using correct host/port for your provider

---

## Production Checklist

Before going live:

- [ ] MailHog removed from production docker-compose
- [ ] Production SMTP service configured (SES, SendGrid, etc.)
- [ ] SMTP credentials stored securely (Key Vault, Secrets Manager)
- [ ] DNS records configured (SPF, DKIM, DMARC)
- [ ] Domain verified with email provider
- [ ] SSL/TLS enabled for SMTP
- [ ] "From" email address verified
- [ ] Test emails sent and received successfully
- [ ] Emails not going to spam
- [ ] Password reset flow tested end-to-end
- [ ] Monitoring configured for bounce/complaint rates
- [ ] Alerts set up for SMTP failures
- [ ] Backup SMTP provider configured (optional but recommended)
- [ ] Email templates customized for production
- [ ] Unsubscribe mechanism implemented (if applicable)
- [ ] Compliance with CAN-SPAM / GDPR

---

## Cost Comparison

| Service | Free Tier | Paid Tier | Best For |
|---------|-----------|-----------|----------|
| **AWS SES** | 62K/month (from EC2) | $0.10/1K | AWS deployments |
| **SendGrid** | 100/day | $19.95/50K | Easy setup |
| **Mailgun** | 5K/month (3 months) | $35/50K | Developers |
| **Azure Comm** | None | $0.25/1K | Azure deployments |
| **Office 365** | Included | Included | Existing O365 |
| **Google Workspace** | 2K/day | Included | Existing Workspace |

---

## Recommended Setup by Deployment

| Deployment | Email Service | Why |
|------------|---------------|-----|
| **AWS** | AWS SES | Native integration, lowest cost |
| **Azure** | Azure Communication Services | Native integration |
| **GCP** | SendGrid or Mailgun | GCP doesn't have native email service |
| **On-Premise** | Office 365 or dedicated SMTP | Existing infrastructure |
| **Small/Startup** | SendGrid Free Tier | Easy, free for low volume |

---

## Related Documentation

- [PASSWORD-SECURITY.md](./PASSWORD-SECURITY.md) - Password reset flow
- [SECURITY-CHECKLIST.md](./SECURITY-CHECKLIST.md) - Security requirements
- [EMAIL-SETUP-DEV.md](./EMAIL-SETUP-DEV.md) - Development email setup

---

**Last Updated**: {Current Date}  
**Version**: 1.0  
**Next Review**: Before production deployment
