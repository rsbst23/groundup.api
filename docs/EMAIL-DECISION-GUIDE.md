# Email Setup Decision Guide

Choose the right email setup for your needs.

## Quick Decision Tree

```
Are you deploying to production?
?
?? YES ? Use AWS SES (or SendGrid, Mailgun)
?        See: EMAIL-SETUP-PRODUCTION.md
?
?? NO (Development Only)
   ?
   ?? Do you want to use the same service as production?
   ?  ?
   ?  ?? YES ? Use AWS SES for both dev and prod
   ?  ?        ? Same configuration everywhere
   ?  ?        ? Production-ready testing
   ?  ?        ?? Need to verify test emails
   ?  ?        See: AWS-SES-SETUP.md
   ?  ?
   ?  ?? NO ? Use MailHog for dev, AWS SES for prod
   ?           ? Easy local testing
   ?           ? No email verification needed
   ?           ? Web UI to view emails
   ?           ?? Different configs for dev/prod
   ?           See: EMAIL-SETUP-DEV.md
   ?
   ?? Are you working offline frequently?
      ?
      ?? YES ? Use MailHog
      ?        ? Works offline
      ?        See: EMAIL-SETUP-DEV.md
      ?
      ?? NO ? Either MailHog or AWS SES works
              See: AWS-SES-SETUP.md or EMAIL-SETUP-DEV.md
```

---

## Option Comparison

### Option 1: AWS SES for Dev & Prod (Recommended)

**Setup**: [AWS-SES-SETUP.md](./AWS-SES-SETUP.md)

**Pros:**
- ? Identical configuration in dev and prod
- ? Real email delivery and testing
- ? Production-ready from day one
- ? No extra Docker services
- ? Very cheap ($0.10 per 1,000 emails)

**Cons:**
- ?? Need to verify test email addresses (in sandbox mode)
- ?? Test emails go to real inbox (clutter)
- ?? Requires AWS account
- ?? Requires internet connection
- ?? More initial setup

**Best For:**
- Small teams (1-5 developers)
- Want production-like testing
- Already using AWS
- Don't mind email inbox clutter

**Cost:** FREE (first 62K emails/month from EC2)

---

### Option 2: MailHog for Dev, AWS SES for Prod

**Setup Dev**: [EMAIL-SETUP-DEV.md](./EMAIL-SETUP-DEV.md)  
**Setup Prod**: [EMAIL-SETUP-PRODUCTION.md](./EMAIL-SETUP-PRODUCTION.md)

**Pros:**
- ? Easy local testing with web UI
- ? No email verification needed
- ? Works offline
- ? No inbox clutter
- ? Fast iteration (view emails instantly)
- ? Free for development

**Cons:**
- ?? Different configurations for dev/prod
- ?? Need to run MailHog container
- ?? Not testing with real email service

**Best For:**
- Larger teams
- Frequent testing during development
- Offline development
- Don't want AWS setup for dev

**Cost:** FREE (MailHog), then AWS SES pricing for prod

---

### Option 3: SendGrid for Dev & Prod

**Setup**: [EMAIL-SETUP-PRODUCTION.md](./EMAIL-SETUP-PRODUCTION.md) (Section: SendGrid)

**Pros:**
- ? Free tier (100 emails/day)
- ? Easy setup (easier than AWS)
- ? Great deliverability
- ? Built-in analytics

**Cons:**
- ?? Lower free tier than AWS SES
- ?? Same issues as AWS SES for dev

**Best For:**
- Want easier setup than AWS
- Don't need high volume
- Like having analytics dashboard

**Cost:** FREE (100 emails/day), then $19.95/month for 50K

---

## Current Setup in This Project

Your project is currently configured for:

### Development
```yaml
# keycloak-compose.yml
# MailHog is commented out - you can use either:

# Option A: Uncomment MailHog section
# Option B: Use AWS SES (configured in .env)
```

### Environment Variables
```env
# .env file includes AWS SES configuration
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_FROM=noreply@groundup.dev
SMTP_USERNAME=YOUR_SES_SMTP_USERNAME
SMTP_PASSWORD=YOUR_SES_SMTP_PASSWORD
```

---

## Switching Between Options

### To Use MailHog

1. **Uncomment MailHog in docker-compose**
   ```yaml
   # keycloak-compose.yml
   mailhog:
     image: mailhog/mailhog:latest
     container_name: groundup-mailhog
     ports:
       - "1025:1025"
       - "8025:8025"
   ```

2. **Configure Keycloak**
   ```
   Host: mailhog
   Port: 1025
   SSL: Disabled
   Auth: Disabled
   ```

3. **Start services**
   ```bash
   docker-compose -f keycloak-compose.yml up -d
   ```

4. **View emails at**: http://localhost:8025

### To Use AWS SES

1. **Follow**: [AWS-SES-SETUP.md](./AWS-SES-SETUP.md)

2. **Update .env** with your SES credentials

3. **Configure Keycloak**
   ```
   Host: email-smtp.us-east-1.amazonaws.com
   Port: 587
   SSL: StartTLS
   Auth: Enabled
   ```

4. **Verify test emails** in SES Console

---

## Our Recommendation

### For Your Situation

Based on your question about using AWS SES for both dev and prod:

**? Yes, absolutely!** This is a great approach.

**Why:**
1. ? **Simpler** - One configuration for both environments
2. ? **Production-Ready** - Test with actual production service
3. ? **Real Testing** - Actually receive emails
4. ? **Cheap** - Free tier covers most development needs
5. ? **No Extra Services** - Don't need to run MailHog

**Setup Steps:**
1. Follow [AWS-SES-SETUP.md](./AWS-SES-SETUP.md)
2. Verify your test email in AWS SES Console
3. Update `.env` with your SES credentials
4. Configure Keycloak SMTP settings
5. Test by creating a user!

---

## Need Help?

### Quick Starts
- **5-min MailHog setup**: [QUICK-START-EMAIL.md](./QUICK-START-EMAIL.md)
- **AWS SES setup**: [AWS-SES-SETUP.md](./AWS-SES-SETUP.md)

### Full Guides
- **Development (MailHog)**: [EMAIL-SETUP-DEV.md](./EMAIL-SETUP-DEV.md)
- **Production (All options)**: [EMAIL-SETUP-PRODUCTION.md](./EMAIL-SETUP-PRODUCTION.md)
- **AWS SES (Both)**: [AWS-SES-SETUP.md](./AWS-SES-SETUP.md)

### Security
- **Password Flow**: [PASSWORD-SECURITY.md](./PASSWORD-SECURITY.md)
- **Security Checklist**: [SECURITY-CHECKLIST.md](./SECURITY-CHECKLIST.md)

---

**Last Updated**: {Current Date}
