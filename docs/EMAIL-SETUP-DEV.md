# Email Testing Setup for Development

**?? DEVELOPMENT ONLY** - This guide is for local development. For production, see [EMAIL-SETUP-PRODUCTION.md](./EMAIL-SETUP-PRODUCTION.md)

This guide explains how to set up email functionality on your local development machine without a real SMTP server.

## Option 1: MailHog (Recommended) ??

**MailHog** is a fake SMTP server that catches all emails and displays them in a web UI. Perfect for testing!

### What is MailHog?

- ? Catches all outgoing emails
- ? Displays them in a web interface
- ? No real emails are sent
- ? Easy to set up with Docker
- ? Zero configuration needed

### Setup Steps

#### 1. MailHog is Already Added to Docker Compose

The `keycloak-compose.yml` file now includes MailHog:

```yaml
mailhog:
  image: mailhog/mailhog:latest
  container_name: groundup-mailhog
  ports:
    - "1025:1025"  # SMTP server
    - "8025:8025"  # Web UI
  networks:
    - keycloak-network
```

#### 2. Start MailHog with Keycloak

```bash
docker-compose -f keycloak-compose.yml up -d
```

This will start:
- Keycloak
- Keycloak MySQL database
- **MailHog** (SMTP server + Web UI)

#### 3. Access MailHog Web UI

Open your browser and navigate to:
```
http://localhost:8025
```

You'll see the MailHog inbox where all emails will appear!

#### 4. Configure Keycloak SMTP Settings

1. **Login to Keycloak Admin Console**
   - URL: `http://localhost:8080`
   - Username: Your `KEYCLOAK_ADMIN` value (from .env)
   - Password: Your `KEYCLOAK_ADMIN_PASSWORD` value (from .env)

2. **Navigate to Email Settings**
   - Select your realm (e.g., "GroundUp")
   - Click **Realm Settings** in left menu
   - Click **Email** tab

3. **Configure SMTP Settings**
   ```
   From: noreply@groundup.local
   From Display Name: GroundUp Application
   Reply To: (leave empty)
   Reply To Display Name: (leave empty)
   Envelope From: (leave empty)
   
   Host: mailhog  (or localhost if running Keycloak outside Docker)
   Port: 1025
   Encryption: No encryption (disabled)
   Authentication: No (disabled)
   ```

   **Important Notes:**
   - If Keycloak is running in Docker: use `mailhog` as the host
   - If Keycloak is running locally (not in Docker): use `localhost` as the host
   - Port must be `1025` (MailHog's SMTP port)
   - **Disable SSL/TLS** for local development
   - **No authentication** required for MailHog

4. **Test Email Configuration**
   - Click **Test connection** button
   - Enter an email address (can be fake like `test@example.com`)
   - Click **Save**

5. **Check MailHog UI**
   - Go to `http://localhost:8025`
   - You should see the test email!

### Testing the Password Reset Flow

1. **Create a User via API**
   ```bash
   curl -X POST http://localhost:5000/api/users \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer YOUR_TOKEN" \
     -d '{
       "username": "testuser",
       "email": "test@example.com",
       "firstName": "Test",
       "lastName": "User",
       "sendWelcomeEmail": true
     }'
   ```

2. **Check MailHog**
   - Open `http://localhost:8025`
   - You should see the password reset email
   - Click the email to view it
   - Click the "Update Password" link (it will open Keycloak)

3. **Set Password**
   - Enter your new password twice
   - Click Submit
   - You can now login with the new password!

### MailHog Features

- **Web UI**: `http://localhost:8025`
- **SMTP Server**: `localhost:1025`
- **View All Emails**: See all captured emails in the inbox
- **Search**: Search through emails
- **Delete**: Clear individual emails or all emails
- **API**: MailHog has a JSON API for automated testing

### MailHog Commands

```bash
# Start MailHog with Keycloak
docker-compose -f keycloak-compose.yml up -d

# View MailHog logs
docker logs groundup-mailhog

# Stop MailHog
docker-compose -f keycloak-compose.yml down

# Restart MailHog
docker-compose -f keycloak-compose.yml restart mailhog
```

---

## Option 2: Papercut SMTP (Windows Alternative)

If you prefer a Windows desktop application instead of Docker:

### Download & Install

1. Download **Papercut SMTP** from: https://github.com/ChangemakerStudios/Papercut-SMTP/releases
2. Install and run the application
3. It will start an SMTP server on `localhost:25`

### Configure Keycloak

```
Host: host.docker.internal  (if Keycloak is in Docker)
      localhost              (if Keycloak is running locally)
Port: 25
Encryption: No
Authentication: No
```

**Note**: To allow Docker to access your Windows SMTP server, use `host.docker.internal` as the hostname.

---

## Option 3: Ethereal Email (Online Service)

**Ethereal** is a fake SMTP service with a web interface (no installation needed).

### Setup

1. Go to https://ethereal.email/
2. Click **Create Ethereal Account**
3. Note the SMTP credentials provided

### Configure Keycloak

```
Host: smtp.ethereal.email
Port: 587
Encryption: TLS
Authentication: Yes
Username: [provided by Ethereal]
Password: [provided by Ethereal]
```

### View Emails

- Login to https://ethereal.email/messages with your credentials
- All emails sent will appear there

**Cons**: 
- Requires internet connection
- Credentials expire after some time
- Not as convenient as local solutions

---

## Option 4: Gmail with App Password (Real Emails)

If you want to send **real emails** for testing:

### Setup

1. **Enable 2-Factor Authentication** on your Gmail account
2. **Create App Password**:
   - Go to Google Account ? Security ? 2-Step Verification ? App passwords
   - Generate a new app password
   - Copy the 16-character password

### Configure Keycloak

```
Host: smtp.gmail.com
Port: 587
From: your-email@gmail.com
Encryption: StartTLS
Authentication: Yes
Username: your-email@gmail.com
Password: [16-character app password]
```

**Warning**: This will send real emails! Only use this if you want actual emails delivered.

---

## Recommended Setup for Development

### Quick Start with MailHog

```bash
# 1. Start services
docker-compose -f keycloak-compose.yml up -d

# 2. Open MailHog UI
# Browser: http://localhost:8025

# 3. Open Keycloak Admin
# Browser: http://localhost:8080

# 4. Configure Keycloak Email Settings
# Realm Settings ? Email
# Host: mailhog
# Port: 1025
# No SSL, No Auth
```

### Verify Setup

1. Test email connection in Keycloak
2. Create a test user via your API
3. Check MailHog UI at `http://localhost:8025`
4. You should see the password reset email!

---

## Troubleshooting

### Email not appearing in MailHog

**Check 1: Is MailHog running?**
```bash
docker ps | grep mailhog
```

**Check 2: Access MailHog UI**
```
http://localhost:8025
```

**Check 3: Check Keycloak logs**
```bash
docker logs groundup-keycloak
```
Look for email-related errors

**Check 4: Verify SMTP settings in Keycloak**
- Host should be `mailhog` (if Keycloak is in Docker)
- Port should be `1025`
- SSL/TLS should be **disabled**
- Authentication should be **disabled**

### Connection refused

If you get "Connection refused":

**If Keycloak is in Docker:**
- Use `mailhog` as the hostname (containers communicate via service name)

**If Keycloak is running locally:**
- Use `localhost` as the hostname
- Or use `host.docker.internal` to access host machine from Docker

### Emails going to spam

This won't happen with MailHog since it's a fake SMTP server. All emails are captured locally.

### MailHog not accessible

**Check port conflicts:**
```bash
# Check if port 8025 is in use
netstat -ano | findstr :8025

# Check if port 1025 is in use
netstat -ano | findstr :1025
```

If ports are in use, you can change them in `keycloak-compose.yml`:
```yaml
mailhog:
  ports:
    - "1026:1025"  # Change external port
    - "8026:8025"  # Change external port
```

---

## Testing Checklist

- [ ] MailHog is running (`docker ps`)
- [ ] MailHog UI is accessible (`http://localhost:8025`)
- [ ] Keycloak SMTP is configured (Realm Settings ? Email)
- [ ] Test connection succeeds in Keycloak
- [ ] Create test user via API with `sendWelcomeEmail: true`
- [ ] Email appears in MailHog UI
- [ ] Password reset link works
- [ ] User can set password successfully

---

## Production vs Development

### Development (MailHog)
```yaml
Host: mailhog
Port: 1025
SSL: No
Auth: No
```

### Production (Real SMTP)
```yaml
Host: smtp.yourprovider.com
Port: 587 or 465
SSL: Yes (TLS/SSL)
Auth: Yes (credentials)
From: noreply@yourdomain.com
```

**Remember**: Update email configuration before deploying to production!

---

## Additional Resources

- [MailHog GitHub](https://github.com/mailhog/MailHog)
- [Keycloak Email Documentation](https://www.keycloak.org/docs/latest/server_admin/#_email)
- [PASSWORD-SECURITY.md](./PASSWORD-SECURITY.md) - Password flow documentation

---

**Last Updated**: {Current Date}  
**Version**: 1.0
