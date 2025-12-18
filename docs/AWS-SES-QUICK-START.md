# ?? Quick Start: AWS SES Email Setup for GroundUp

## Overview

This guide will help you quickly set up AWS SES (Simple Email Service) for sending emails from your Keycloak realms.

---

## ?? What You'll Accomplish

- ? Create SMTP credentials in AWS SES
- ? Verify your email address (or domain)
- ? Configure GroundUp API to use AWS SES
- ? Test email sending
- ? (Optional) Request production access

---

## ?? Prerequisites

- AWS Account with access to SES
- Email address or domain to verify
- Access to GroundUp API `.env` file

---

## Step 1: Access AWS SES Console

1. Log in to AWS Console: https://console.aws.amazon.com
2. Navigate to **SES** (Simple Email Service)
   - Search for "SES" in the top search bar
   - Or go directly: https://console.aws.amazon.com/ses
3. **Select your region** (e.g., `us-east-1`)
   - ?? Remember this region - you'll need it for SMTP host

---

## Step 2: Verify Email Address

### Option A: Verify Single Email (Quick Start)

1. In SES Console, go to **Verified identities**
2. Click **Create identity**
3. Select **Email address**
4. Enter your email: `robertsbeck1979@gmail.com`
5. Click **Create identity**
6. **Check your email** for verification link
7. Click the verification link
8. Wait for status to show ? **Verified**

### Option B: Verify Domain (Production)

1. In SES Console, go to **Verified identities**
2. Click **Create identity**
3. Select **Domain**
4. Enter your domain: `yourdomain.com`
5. Choose **Easy DKIM**
6. Click **Create identity**
7. **Add DNS records** shown in AWS Console to your domain
8. Wait for verification (can take up to 72 hours)

---

## Step 3: Create SMTP Credentials

1. In SES Console, go to **SMTP settings** (left sidebar)
2. Click **Create SMTP credentials**
3. Enter a name: `GroundUp-SMTP-User`
4. Click **Create user**
5. **Download credentials** or copy them immediately
   - ?? This is your **only chance** to see the password
6. Save these values:
   ```
   SMTP Username: AKIAXXXXXXXXXXXXXXXX
   SMTP Password: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
   ```

---

## Step 4: Get Your SMTP Host

Your SMTP host depends on your AWS region:

| Region | SMTP Host |
|--------|-----------|
| `us-east-1` | `email-smtp.us-east-1.amazonaws.com` |
| `us-west-2` | `email-smtp.us-west-2.amazonaws.com` |
| `eu-west-1` | `email-smtp.eu-west-1.amazonaws.com` |

**Full list**: https://docs.aws.amazon.com/ses/latest/dg/smtp-connect.html

---

## Step 5: Configure GroundUp API

Edit `GroundUp.api/.env`:

```sh
# SMTP Configuration for Keycloak Realms
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_FROM=robertsbeck1979@gmail.com
SMTP_FROM_DISPLAY_NAME=GroundUp Application
SMTP_REPLY_TO=robertsbeck1979@gmail.com
SMTP_ENVELOPE_FROM=
SMTP_AUTH_ENABLED=true
SMTP_STARTTLS_ENABLED=true
SMTP_SSL_ENABLED=false
SMTP_USERNAME=AKIAYDPXGOGHDSXIKEP6
SMTP_PASSWORD=YOUR_ACTUAL_SMTP_PASSWORD_HERE
```

**Replace**:
- `YOUR_ACTUAL_SMTP_PASSWORD_HERE` with the password from Step 3
- `robertsbeck1979@gmail.com` with your verified email
- `email-smtp.us-east-1.amazonaws.com` with your region's SMTP host

---

## Step 6: Restart API

```bash
# If using Docker
docker-compose restart api

# If running locally
# Stop (Ctrl+C) and restart:
dotnet run --project GroundUp.api/GroundUp.api.csproj
```

---

## Step 7: Test Email Sending

### Method 1: Create Test Enterprise Tenant

1. Open Swagger: `http://localhost:5000/swagger`
2. Find `POST /api/tenants/enterprise/signup`
3. Create a test tenant
4. Check if realm was created with SMTP configured

### Method 2: Test in Keycloak Admin

1. Go to Keycloak Admin: `http://localhost:8080/admin`
2. Select a realm with SMTP configured
3. Go to **Realm Settings** ? **Email** tab
4. Scroll to bottom
5. Click **Test connection**
6. Enter a test email address
7. Check if email is received

---

## ?? Sandbox Limitations

**New AWS SES accounts are in "Sandbox Mode"** with restrictions:

### Restrictions:
- ? Can send to **verified email addresses only**
- ? Can send to **verified domains only**
- ? Cannot send to arbitrary email addresses
- ?? Limited to 200 emails/day
- ?? Limited to 1 email/second

### During Testing:
1. **Verify the test recipient email** in SES Console
2. Use only verified emails for testing
3. Invitation emails will work as long as recipient email is verified

### For Production:
You'll need to **request production access** (see Step 8)

---

## Step 8: Request Production Access (Optional)

**Only needed when ready to send to any email address**

1. In SES Console, click **Request production access** (top banner)
2. Fill out the form:
   - **Mail type**: Transactional
   - **Website URL**: `https://yourapp.com`
   - **Use case description**:
     ```
     We are building a multi-tenant SaaS application that sends
     transactional emails including:
     - Account verification emails
     - Password reset emails
     - Tenant invitation emails
     - System notifications
     
     Estimated volume: [X emails/day]
     ```
3. Submit the request
4. AWS typically responds within 24 hours

---

## ? Verification Checklist

- [ ] AWS SES account created
- [ ] Email address (or domain) verified ?
- [ ] SMTP credentials created and saved
- [ ] SMTP host identified for your region
- [ ] `.env` file updated with SMTP settings
- [ ] API restarted
- [ ] Test email sent successfully
- [ ] (Optional) Production access requested

---

## ?? Troubleshooting

### "Email address is not verified"

**Solution**: Verify the sender email in SES Console
- Go to **Verified identities** ? **Create identity**
- Verify the email address
- Wait for verification email and click link

### "MessageRejected: Email address is not verified"

**Solution**: You're in sandbox mode
- Either verify the recipient email in SES
- Or request production access

### "Authentication credentials invalid"

**Solution**: Check SMTP credentials
- Make sure you're using SMTP credentials (not AWS access keys)
- Regenerate SMTP credentials if needed
- Double-check username and password in `.env`

### "Connection timeout"

**Solution**: Check SMTP host and port
- Verify you're using the correct region's SMTP host
- Port should be `587` (StartTLS) or `465` (SSL)
- Check firewall/network settings

### Emails going to spam

**Solution**: Set up SPF and DKIM records
- AWS provides DKIM records when verifying domain
- Add SPF record: `v=spf1 include:amazonses.com ~all`
- Configure custom MAIL FROM domain (optional)

---

## ?? Additional Resources

- [AWS SES Documentation](https://docs.aws.amazon.com/ses/)
- [SES SMTP Endpoints](https://docs.aws.amazon.com/ses/latest/dg/smtp-connect.html)
- [Moving out of Sandbox](https://docs.aws.amazon.com/ses/latest/dg/request-production-access.html)
- [Email Verification Configuration](./EMAIL-VERIFICATION-CONFIGURATION.md)

---

## ?? Pro Tips

1. **Use a dedicated email for transactional emails**: `noreply@yourdomain.com`
2. **Set up SPF/DKIM early** to avoid spam issues
3. **Monitor sending limits** in SES Console
4. **Use configuration sets** for tracking bounces/complaints
5. **Test with verified emails** in sandbox mode
6. **Request production access early** (can take 24 hours)

---

**Status:** ? **READY TO USE**  
**Estimated Time:** 15-30 minutes  
**Difficulty:** Beginner  
**Cost:** AWS Free Tier (62,000 emails/month free)
