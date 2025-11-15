# Quick Start: Email Testing with MailHog

**?? DEVELOPMENT ONLY** - Do not use MailHog in production! See [EMAIL-SETUP-PRODUCTION.md](./EMAIL-SETUP-PRODUCTION.md) for production email configuration.

## ?? 5-Minute Setup

### Step 1: Start MailHog (Already Configured!)
```bash
docker-compose -f keycloak-compose.yml up -d
```

### Step 2: Open MailHog Web Interface
Open your browser: **http://localhost:8025**

You should see an empty inbox - this is where all emails will appear!

### Step 3: Configure Keycloak SMTP

1. Open Keycloak Admin Console: **http://localhost:8080**
   - Login with your admin credentials

2. Go to **Realm Settings** ? **Email** tab

3. Enter these settings:
   ```
   From: noreply@groundup.local
   From Display Name: GroundUp Application
   Host: mailhog
   Port: 1025
   ```

4. **Important**: 
   - Disable **Enable SSL** (uncheck)
   - Disable **Enable Authentication** (uncheck)

5. Click **Save**

6. Click **Test connection** button
   - Enter any email (e.g., `test@example.com`)
   - Click **Send test email**

7. Check MailHog UI at **http://localhost:8025** - you should see the test email!

### Step 4: Test User Creation

Create a user via your API:

```bash
POST http://localhost:5000/api/users
Content-Type: application/json
Authorization: Bearer YOUR_TOKEN

{
  "username": "testuser",
  "email": "test@example.com",
  "firstName": "Test",
  "lastName": "User",
  "sendWelcomeEmail": true
}
```

### Step 5: Check the Email

1. Go to **http://localhost:8025**
2. You should see a new email from GroundUp
3. Click it to view
4. Click the password reset link
5. Set your password!

## ?? Done!

You now have a fully functional email testing environment!

### Quick Access URLs

- **MailHog Inbox**: http://localhost:8025
- **Keycloak Admin**: http://localhost:8080
- **Your API**: http://localhost:5000

### Common Issues

**Email not showing up?**
- Check MailHog is running: `docker ps | grep mailhog`
- Verify Keycloak host is `mailhog` (not `localhost`)
- Make sure SSL and Authentication are disabled

**Can't access MailHog UI?**
- Make sure port 8025 isn't blocked
- Try: http://127.0.0.1:8025

## What Next?

Read the full guide: **[EMAIL-SETUP-DEV.md](./EMAIL-SETUP-DEV.md)**
