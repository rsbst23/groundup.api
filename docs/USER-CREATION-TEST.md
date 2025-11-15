# User Creation and Password Reset Test Script

This script tests the complete user creation flow with AWS SES email delivery.

## Prerequisites

- ? AWS SES configured and working
- ? Keycloak running
- ? API running
- ? Valid verified email address in AWS SES

---

## Step 1: Get Authorization Token

### Using curl:
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "admin"
  }'
```

### Using Postman:
```
POST http://localhost:5000/api/auth/login
Body (JSON):
{
  "username": "admin",
  "password": "admin"
}
```

**Expected Response:**
```json
{
  "token": "eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldU...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCIgOiAiSldU...",
  "expiresIn": 300
}
```

**?? Copy the `token` value for next steps.**

---

## Step 2: Create a New User

### Using curl:
```bash
curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -d '{
    "username": "johndoe",
    "email": "YOUR_VERIFIED_EMAIL@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "enabled": true,
    "sendWelcomeEmail": true
  }'
```

### Using Postman:
```
POST http://localhost:5000/api/users
Headers:
  Authorization: Bearer YOUR_TOKEN_HERE
  Content-Type: application/json

Body (JSON):
{
  "username": "johndoe",
  "email": "YOUR_VERIFIED_EMAIL@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "enabled": true,
  "sendWelcomeEmail": true
}
```

**?? Important:** Replace `YOUR_VERIFIED_EMAIL@example.com` with one of your verified AWS SES emails:
- rsbt23@yahoo.com ?
- robertsbeck1979@gmail.com ?
- administrator@thecredittoolbox.com ?

**Expected Response:**
```json
{
  "data": {
    "id": "abc123-def456-ghi789",
    "username": "johndoe",
    "email": "rsbt23@yahoo.com",
    "firstName": "John",
    "lastName": "Doe",
    "enabled": true,
    "emailVerified": false
  },
  "success": true,
  "message": "User created successfully",
  "statusCode": 201
}
```

---

## Step 3: Check Email

1. **Check your inbox** at the email address you used
2. **Subject**: Should be something like "Update Your Account" or "Set Up Your Password"
3. **From**: GroundUp Application (or your configured sender)
4. **Check spam folder** if not in inbox

**Sample Email Content:**
```
Hi John,

Someone has requested to update your account. If this was you, 
please click the link below to update your password:

[Update Password] (This is a clickable link)

This link will expire in 5 minutes.

If you did not request this, please ignore this email.

Thanks,
GroundUp Team
```

---

## Step 4: Click Password Reset Link

1. **Click the link** in the email
2. **Keycloak page opens** asking you to set a password
3. **Enter password** (must meet requirements):
   - Minimum 8 characters
   - At least 1 uppercase letter
   - At least 1 lowercase letter
   - At least 1 number
   - At least 1 special character

4. **Enter password again** to confirm
5. **Click Submit**

**Expected Result:**
- Success message: "Your password has been updated"
- Redirected to login page or confirmation page

---

## Step 5: Test Login with New User

### Using curl:
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "johndoe",
    "password": "YourNewPassword123!"
  }'
```

### Using Postman:
```
POST http://localhost:5000/api/auth/login
Body (JSON):
{
  "username": "johndoe",
  "password": "YourNewPassword123!"
}
```

**Expected Response:**
```json
{
  "token": "eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldU...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCIgOiAiSldU...",
  "expiresIn": 300
}
```

**? Success!** The user can now login and access the application.

---

## Step 6: Verify User Has Default Role

### Get User's Roles:
```bash
curl -X GET "http://localhost:5000/api/users/{userId}/system-roles" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**Expected Response:**
```json
{
  "data": [
    {
      "id": "role-id-123",
      "name": "USER",
      "description": "Standard user role",
      "isClientRole": false
    }
  ],
  "success": true,
  "message": "Success",
  "statusCode": 200
}
```

**? The user should have the "USER" role automatically assigned!**

---

## Troubleshooting

### Issue: "Failed to send email"

**Possible Causes:**
1. **Keycloak SMTP not configured correctly**
   - Check: Realm Settings ? Email
   - Verify: Host, Port, Username, Password
   - Ensure: SSL is unchecked, StartTLS is checked

2. **AWS SES sandbox mode**
   - Verify the recipient email in AWS SES Console
   - Both FROM and TO emails must be verified

3. **SMTP credentials expired or wrong**
   - Regenerate SMTP credentials in AWS SES
   - Update Keycloak with new credentials

**Check Keycloak Logs:**
```bash
docker logs groundup-keycloak | grep -i email
```

---

### Issue: Email goes to spam

**Solutions:**
1. Check spam/junk folder
2. Mark as "Not Spam"
3. Add sender to contacts
4. (Production) Configure SPF/DKIM records

---

### Issue: "Unauthorized" error when creating user

**Cause:** Invalid or expired token

**Solution:**
1. Get a new token from `/api/auth/login`
2. Make sure to include `Authorization: Bearer TOKEN` header
3. Check token hasn't expired (5 minutes by default)

---

### Issue: Password reset link expired

**Cause:** Link is only valid for 5 minutes (Keycloak default)

**Solution:**
1. Request another password reset email
2. Click link immediately
3. (Optional) Increase token lifespan in Keycloak:
   - Realm Settings ? Tokens ? Action Token Lifespan

---

### Issue: Can't set password - "Password doesn't meet requirements"

**Check Keycloak Password Policy:**
1. Keycloak Admin Console
2. Authentication ? Policies ? Password Policy
3. Review requirements

**Default Requirements:**
- Minimum 8 characters
- At least 1 uppercase
- At least 1 lowercase  
- At least 1 digit
- At least 1 special character

---

## Verification Checklist

After running the test:

- [ ] User created in Keycloak (check Keycloak Admin Console ? Users)
- [ ] User saved in local database (check database `Users` table)
- [ ] User assigned to tenant (check `UserTenants` table)
- [ ] User has "USER" role in Keycloak
- [ ] Email received in inbox
- [ ] Password reset link works
- [ ] User can login with new password
- [ ] Login returns valid JWT token
- [ ] Token can be used for API requests

---

## What Happens Behind the Scenes

When you create a user, here's what happens:

1. **API receives request** ? `UserController.Create()`
2. **Creates user in Keycloak** ? `IdentityProviderAdminService.CreateUserAsync()`
   - Generates random temporary password
   - Sets `temporary: true` flag
   - Adds `UPDATE_PASSWORD` required action
3. **Sends password reset email** ? `SendPasswordResetEmailAsync()`
   - Keycloak sends email via AWS SES
4. **Assigns default role** ? `AssignRoleToUserAsync("USER")`
5. **Saves to local database** ? Insert into `Users` table
6. **Assigns to tenant** ? Insert into `UserTenants` table
7. **Returns success** ? API response with user details

---

## Cost Estimate

For testing (assuming ~10 test users):
- **Emails sent**: ~10 emails
- **AWS SES Cost**: FREE (under 62K/month)
- **Total**: $0.00

---

## Next Steps After Successful Test

Once the test succeeds:

1. ? **Update documentation** with your specific configuration
2. ? **Test with multiple verified emails** (team members)
3. ? **Request production access** from AWS SES (when ready)
4. ? **Configure DNS records** (SPF, DKIM) for production
5. ? **Customize email templates** in Keycloak (optional)
6. ? **Set up monitoring** for email delivery rates

---

## Related Documentation

- [PASSWORD-SECURITY.md](./PASSWORD-SECURITY.md) - Password security implementation
- [AWS-SES-SETUP.md](./AWS-SES-SETUP.md) - AWS SES configuration guide
- [SECURITY-CHECKLIST.md](./SECURITY-CHECKLIST.md) - Production security checklist

---

**Last Updated**: {Current Date}  
**Version**: 1.0
