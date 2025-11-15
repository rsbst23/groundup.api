# AWS SES Setup for Development and Production

This guide shows how to use **AWS SES for both development and production** instead of MailHog.

## ? Why Use AWS SES for Both?

| Benefit | Description |
|---------|-------------|
| ? **Consistent Configuration** | Same setup in dev and prod |
| ? **Real Email Testing** | Actually receive emails |
| ? **Production-Ready** | Test with actual production service |
| ? **Simple Setup** | No extra services to run |
| ? **Very Cheap** | First 62K emails/month free (from EC2), then $0.10/1K |

## ?? Prerequisites

- AWS Account (free tier available)
- Email address for testing (Gmail, etc.)

---

## ?? Setup Steps

### Step 1: Create AWS Account & Setup SES

1. **Sign up for AWS** (if you don't have an account)
   - Go to https://aws.amazon.com
   - Create free tier account

2. **Navigate to SES**
   - AWS Console ? Services ? Simple Email Service (SES)
   - Choose your region (e.g., `us-east-1`, `us-west-2`)
   - **Important**: Remember your region - you'll need it for SMTP endpoint

### Step 2: Verify Your Email Address (Sandbox Mode)

AWS SES starts in **sandbox mode** - you can only send emails to verified addresses.

1. **Verify Email Address**
   ```
   SES Console ? Verified identities ? Create identity
   
   Identity type: Email address
   Email address: your-email@gmail.com
   
   Click "Create identity"
   ```

2. **Check Your Inbox**
   - AWS will send verification email
   - Click the verification link
   - Status will change to "Verified"

3. **Verify Additional Emails** (for team members)
   - Repeat for each developer's email
   - Each person who tests locally needs their email verified

**Sandbox Limitations:**
- ?? Can only send TO verified emails
- ?? Can only send FROM verified emails
- ?? Maximum 200 emails per 24 hours
- ?? Maximum 1 email per second

**For Production:** Request production access (see Step 6)

### Step 3: Create SMTP Credentials

1. **Generate SMTP Credentials**
   ```
   SES Console ? Account dashboard (left menu)
   ? SMTP Settings ? Create SMTP credentials
   
   IAM User Name: ses-smtp-user-groundup
   
   Click "Create user"
   ```

2. **Download Credentials**
   - **Important**: Download the CSV file with credentials
   - You won't be able to see the password again!
   - Save securely (never commit to Git!)

3. **Note Your SMTP Settings**
   ```
   SMTP endpoint: email-smtp.us-east-1.amazonaws.com (depends on your region)
   Port: 587 (TLS) or 465 (SSL)
   SMTP Username: [from downloaded CSV]
   SMTP Password: [from downloaded CSV]
   ```

**Regional SMTP Endpoints:**
```
us-east-1: email-smtp.us-east-1.amazonaws.com
us-west-2: email-smtp.us-west-2.amazonaws.com
eu-west-1: email-smtp.eu-west-1.amazonaws.com
ap-southeast-1: email-smtp.ap-southeast-1.amazonaws.com
```

### Step 4: Configure Environment Variables

1. **Update `.env` file** (already done):
   ```env
   # AWS SES SMTP Configuration
   SMTP_HOST=email-smtp.us-east-1.amazonaws.com
   SMTP_PORT=587
   SMTP_FROM=noreply@groundup.dev  # Use your verified email
   SMTP_FROM_DISPLAY_NAME=GroundUp Application
   SMTP_USERNAME=YOUR_SES_SMTP_USERNAME
   SMTP_PASSWORD=YOUR_SES_SMTP_PASSWORD
   ```

2. **Replace Placeholders**:
   - `SMTP_HOST`: Your regional endpoint
   - `SMTP_FROM`: Your verified email address
   - `SMTP_USERNAME`: From downloaded CSV
   - `SMTP_PASSWORD`: From downloaded CSV

3. **Never Commit Real Credentials**
   ```bash
   # Make sure .env is in .gitignore
   echo ".env" >> .gitignore
   ```

### Step 5: Configure Keycloak

1. **Start Keycloak**
   ```bash
   docker-compose -f keycloak-compose.yml up -d
   ```

2. **Login to Keycloak Admin Console**
   - URL: http://localhost:8080
   - Username: admin (from your .env)
   - Password: adminpassword (from your .env)

3. **Navigate to Email Settings**
   ```
   Select Realm: GroundUp (or your realm)
   ? Realm Settings ? Email tab
   ```

4. **Configure SMTP**
   ```
   From: noreply@groundup.dev (or your verified email)
   From Display Name: GroundUp Application
   Reply To: (leave empty or use verified email)
   Reply To Display Name: (leave empty)
   Envelope From: (leave empty)
   
   Host: email-smtp.us-east-1.amazonaws.com
   Port: 587
   Enable StartTLS: ? Enabled
   Enable SSL: ? Disabled (StartTLS is enough)
   Enable Authentication: ? Enabled
   Username: [Your SMTP username]
   Password: [Your SMTP password]
   ```

5. **Test Connection**
   - Click "Test connection" button
   - Enter your verified email address
   - Click "Send test email"
   - Check your inbox!

### Step 6: Request Production Access (When Ready)

For production, you need to move out of sandbox mode.

1. **Request Production Access**
   ```
   SES Console ? Account dashboard
   ? Request production access
   
   Fill out the form:
   - Mail type: Transactional
   - Website URL: https://yourapp.com
   - Use case description: "Password reset emails for user authentication"
   - Compliance: Explain how you handle bounces/complaints
   - Expected send rate: Estimate emails/day
   ```

2. **Wait for Approval**
   - Usually approved within 24 hours
   - AWS may ask follow-up questions

3. **After Approval**
   - ? Send to any email address
   - ? Higher sending limits (start with 200/day, request increases)
   - ? No verification required for recipients

### Step 7: Configure DNS (Production Only)

For production, configure domain authentication:

1. **Verify Your Domain**
   ```
   SES Console ? Verified identities ? Create identity
   
   Identity type: Domain
   Domain: yourdomain.com
   
   Click "Create identity"
   ```

2. **Add DNS Records**
   - AWS will provide DNS records
   - Add to your DNS provider (Route53, Cloudflare, etc.)
   - Records include: DKIM, MAIL FROM, DMARC

3. **Wait for Verification**
   - Usually takes a few minutes to 72 hours
   - Check status in SES console

---

## ?? Testing

### Development Testing

1. **Create a Test User**
   ```bash
   POST http://localhost:5000/api/users
   Content-Type: application/json
   Authorization: Bearer YOUR_TOKEN
   
   {
     "username": "testuser",
     "email": "your-verified-email@gmail.com",
     "firstName": "Test",
     "lastName": "User",
     "sendWelcomeEmail": true
   }
   ```

2. **Check Your Email**
   - Check inbox (and spam folder)
   - You should receive password reset email
   - Click the link to set password

3. **Verify Password Reset Works**
   - Click link in email
   - Set your password
   - Try logging in

### Production Testing

Same as development, but:
- Can send to any email address
- No verification required
- Higher rate limits

---

## ?? Cost Estimation

### Development
```
Estimated emails per day: ~10-50
Monthly cost: FREE (under 62K/month from EC2)
```

### Production (Small App)
```
Users: 1,000
Password resets: ~50/month
Notifications: ~500/month
Total: ~550/month

Cost: FREE (under 62K/month)
If over: ~$0.10 per 1,000 = $0.06/month
```

### Production (Medium App)
```
Users: 10,000
Email volume: ~5,000/month

Cost: FREE (under 62K if from EC2)
If not from EC2: $0.50/month
```

**AWS SES is incredibly cheap!**

---

## ?? Configuration Files

### `.env` (Development)
```env
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_FROM=dev-noreply@groundup.dev
SMTP_FROM_DISPLAY_NAME=GroundUp Dev
SMTP_USERNAME=AKIA...
SMTP_PASSWORD=BH8b...
```

### `.env.production` (Production)
```env
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_FROM=noreply@groundup.com
SMTP_FROM_DISPLAY_NAME=GroundUp
SMTP_USERNAME=AKIA...  # Different credentials
SMTP_PASSWORD=BH8b...   # From AWS Secrets Manager
```

---

## ??? Troubleshooting

### Email Not Received

**Check 1: Sandbox Mode**
- Verify recipient email is in verified identities
- SES Console ? Verified identities

**Check 2: Spam Folder**
- Check spam/junk folder
- Mark as "Not Spam" if needed

**Check 3: SES Sending Statistics**
```
SES Console ? Account dashboard ? Sending statistics
Look for:
- Bounces (should be 0%)
- Complaints (should be 0%)
- Delivery failures
```

**Check 4: Keycloak Logs**
```bash
docker logs groundup-keycloak
```
Look for email errors

### "554 Message rejected: Email address is not verified"

**Problem**: Trying to send to unverified email in sandbox mode

**Solution**:
1. Verify the recipient's email in SES Console
2. Or request production access

### Authentication Failed

**Problem**: Wrong SMTP credentials

**Solution**:
1. Verify username/password in .env match downloaded CSV
2. Generate new SMTP credentials if lost
3. Check for extra spaces in credentials

### Connection Timeout

**Problem**: Firewall blocking port 587

**Solution**:
1. Check firewall rules
2. Try port 465 (SSL) instead of 587 (TLS)
3. Check security group rules (if in AWS)

---

## ?? Security Best Practices

### Never Commit Credentials

```gitignore
# .gitignore
.env
.env.local
.env.production
*.csv
```

### Use AWS Secrets Manager (Production)

Instead of environment variables in production:

```bash
# Store in Secrets Manager
aws secretsmanager create-secret \
  --name groundup/smtp/credentials \
  --secret-string '{"username":"AKIA...","password":"BH8b..."}'
```

### Rotate Credentials Regularly

```bash
# Generate new credentials
SES Console ? SMTP Settings ? Create SMTP credentials

# Update in all environments
# Delete old IAM user
```

### Monitor for Abuse

```bash
# Set up CloudWatch alarms
- Bounce rate > 5%
- Complaint rate > 0.1%
- Unusual sending volume
```

---

## ?? Comparison: MailHog vs AWS SES

| Feature | MailHog | AWS SES |
|---------|---------|---------|
| **Real Email Delivery** | ? No | ? Yes |
| **Free** | ? Yes | ? Yes (62K/month) |
| **Setup Complexity** | ? Easy | ?? Moderate |
| **Production Ready** | ? No | ? Yes |
| **Email UI** | ? Web UI | ? Real inbox |
| **Same Config Dev/Prod** | ? No | ? Yes |
| **Team Testing** | ? Easy | ?? Need verification |
| **Requires Internet** | ? No | ? Yes |

---

## ?? When to Use AWS SES for Dev

**Good For:**
- ? Small teams
- ? Want prod-like environment
- ? Real email testing
- ? Don't mind inbox clutter
- ? Have AWS account anyway

**Not Good For:**
- ? Large teams (many verifications)
- ? Frequent testing (inbox spam)
- ? No AWS account
- ? Offline development
- ? Don't want to manage AWS

---

## ?? Additional Resources

- [AWS SES Documentation](https://docs.aws.amazon.com/ses/)
- [AWS SES Pricing](https://aws.amazon.com/ses/pricing/)
- [AWS SES SMTP Endpoints](https://docs.aws.amazon.com/ses/latest/dg/smtp-connect.html)
- [Moving Out of Sandbox](https://docs.aws.amazon.com/ses/latest/dg/request-production-access.html)

---

**Last Updated**: {Current Date}  
**Version**: 1.0  
**Related Documents**:
- [EMAIL-SETUP-PRODUCTION.md](./EMAIL-SETUP-PRODUCTION.md)
- [PASSWORD-SECURITY.md](./PASSWORD-SECURITY.md)
