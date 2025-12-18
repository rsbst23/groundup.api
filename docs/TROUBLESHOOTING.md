# Troubleshooting Guide

Solutions to common issues with the authentication system.

---

## ?? **Table of Contents**

1. [Keycloak Issues](#keycloak-issues)
2. [API Issues](#api-issues)
3. [React Issues](#react-issues)
4. [Authentication Flow Issues](#authentication-flow-issues)
5. [Database Issues](#database-issues)
6. [Common Errors](#common-errors)

---

## ?? **Keycloak Issues**

### **"Redirect URI mismatch"**

**Full Error:**
```
Invalid parameter: redirect_uri
```

**Cause:**
The redirect URI in your request doesn't match Keycloak configuration.

**Solution:**

1. Open Keycloak Admin Console: `http://localhost:8080/admin`
2. Go to **Clients** ? `groundup-api` ? **Settings**
3. Check **Valid redirect URIs:**
   ```
   http://localhost:5173/auth/callback*
   ```
4. The `*` at the end is **required**!
5. Click **"Save"**
6. Restart your React app

**Verify:**
```
# Should work:
http://localhost:5173/auth/callback
http://localhost:5173/auth/callback?code=123&state=abc

# Won't work (missing *):
http://localhost:5173/auth/callback
```

---

### **"Invalid client credentials"**

**Full Error:**
```
Failed to exchange authorization code for tokens
Status: 401 Unauthorized
```

**Cause:**
Client secret in `.env` doesn't match Keycloak or client authentication is disabled.

**Solution:**

**Step 1: Verify Client Authentication**
1. Keycloak Admin ? **Clients** ? `groundup-api`
2. **Capability config** tab
3. **Client authentication:** Must be ? ON
4. Click **"Save"**

**Step 2: Get Correct Client Secret**
1. **Credentials** tab (only visible if authentication is ON)
2. Copy **Client secret**
3. Update `.env`:
   ```bash
   KEYCLOAK_CLIENT_SECRET=abc123def456...
   ```
4. Restart API

**Step 3: Verify No Extra Characters**
```bash
# ? Correct
KEYCLOAK_CLIENT_SECRET=abc123

# ? Wrong - has quotes
KEYCLOAK_CLIENT_SECRET="abc123"

# ? Wrong - has spaces
KEYCLOAK_CLIENT_SECRET= abc123
```

**Test:**
```bash
curl -X POST 'http://localhost:8080/realms/groundup/protocol/openid-connect/token' \
  -H 'Content-Type: application/x-form-urlencoded' \
  -d 'grant_type=client_credentials' \
  -d 'client_id=groundup-api' \
  -d 'client_secret=YOUR_SECRET_HERE'
```

Should return an access token.

---

### **"User registration not available"**

**Symptom:**
No "Register" link on Keycloak login page.

**Solution:**

1. Keycloak Admin ? **Realm settings** ? **Login** tab
2. **User registration:** Turn ? ON
3. Click **"Save"**
4. Refresh login page in browser

---

### **"Email not verified" errors**

**Full Error:**
```
User email not verified
```

**Cause:**
Email verification is required but user hasn't verified their email.

**Solutions:**

**Option 1: Disable for Testing**
1. **Realm settings** ? **Login** tab
2. **Verify email:** Turn ? OFF
3. Click **"Save"**

**Option 2: Manually Verify User**
1. **Users** ? Select user
2. **Details** tab
3. **Email verified:** Turn ? ON
4. Click **"Save"**

**Option 3: Configure Email Server** (Production)
1. **Realm settings** ? **Email** tab
2. Configure SMTP settings
3. Click **"Save"** and **"Test connection"**

---

### **"Standard flow is disabled"**

**Full Error:**
```
Client is not allowed to initiate browser login with given response_type
```

**Cause:**
OAuth standard flow not enabled.

**Solution:**

1. **Clients** ? `groundup-api`
2. **Capability config** tab
3. **Standard flow:** Must be ? ON
4. Click **"Save"**

---

## ?? **API Issues**

### **"Cannot connect to Keycloak"**

**Symptoms:**
- API can't reach Keycloak
- Timeout errors
- Connection refused

**Solution:**

**Step 1: Check Keycloak is Running**
```bash
docker ps | grep keycloak
```

Should show running container.

**Step 2: Check Keycloak Port**
```bash
curl http://localhost:8080
```

Should return Keycloak HTML.

**Step 3: Check .env Configuration**
```bash
KEYCLOAK_AUTH_SERVER_URL=http://localhost:8080
KEYCLOAK_REALM=groundup
```

**Step 4: Restart Services**
```bash
# Restart Keycloak
docker-compose -f keycloak-compose.yml restart

# Restart API
# Ctrl+C in terminal, then:
dotnet run
```

---

### **"User sync failed"**

**Error in Logs:**
```
Failed to sync user {userId} to local database
```

**Causes:**
- Database connection issue
- Missing user data from Keycloak
- Validation errors

**Solution:**

**Check Database Connection:**
```bash
# Test connection string in .env
dotnet ef database update
```

**Check Keycloak User Data:**
1. Keycloak Admin ? **Users**
2. Find the user
3. Verify they have Email, First Name, Last Name

**Check API Logs:**
```bash
# Look for detailed error message
# Should show validation errors
```

**Manually Sync User:**
```sql
-- Check if user exists in Keycloak but not DB
SELECT * FROM Users WHERE Id = 'keycloak-user-id';

-- If missing, user sync should happen on next login
```

---

### **"Failed to exchange code for tokens"**

**Error:**
```
Token exchange failed
```

**Causes:**
- OAuth code expired (~60 seconds)
- Code already used (single-use)
- Wrong client secret
- Wrong redirect URI

**Solution:**

1. **Get Fresh Code:**
   - Navigate to Keycloak again
   - Login/Register
   - Copy new code immediately

2. **Verify Client Secret:**
   - Check `.env` matches Keycloak
   - No quotes or spaces

3. **Check Redirect URI:**
   - Must match exactly what was sent to Keycloak

4. **Check Code Hasn't Been Used:**
   - OAuth codes are single-use
   - Get a new one

---

## ?? **React Issues**

### **CORS Errors**

**Error in Browser Console:**
```
CORS policy: No 'Access-Control-Allow-Origin' header is present
```

**Cause:**
Web origins not configured in Keycloak.

**Solution:**

1. Keycloak Admin ? **Clients** ? `groundup-api`
2. **Settings** ? **Web origins:**
   ```
   http://localhost:5173
   http://localhost:5123
   +
   ```
3. Click **"Save"**

---

### **Cookie Not Set**

**Symptom:**
Auth cookie doesn't appear in browser.

**Causes:**
- API not setting cookie correctly
- React not sending `credentials: 'include'`
- SameSite restrictions

**Solution:**

**Check React API Calls:**
```typescript
fetch('http://localhost:5123/api/auth/callback', {
  credentials: 'include',  // ? MUST HAVE THIS
  headers: { 'Accept': 'application/json' }
});
```

**Check Browser DevTools:**
1. Application tab ? Cookies
2. Look for `AuthToken` cookie
3. Should have `HttpOnly`, `Secure` flags

**Check API is Setting Cookie:**
- API logs should show "Token generated and cookie set"
- Response headers should include `Set-Cookie`

---

### **Infinite Redirect Loop**

**Symptom:**
Page keeps redirecting between React and Keycloak.

**Causes:**
- OAuth code not being extracted correctly
- API not returning successful response
- React navigation logic issue

**Solution:**

**Check React AuthCallback Page:**
```typescript
const code = searchParams.get('code');

if (!code) {
  // Handle error - no infinite redirect
  setError('No authorization code received');
  return;
}
```

**Check API Response:**
- Should return `success: true`
- Should include tenant info
- Check browser Network tab

**Add Loading State:**
```typescript
const [loading, setLoading] = useState(true);

if (loading) {
  return <div>Processing...</div>;
}
```

---

## ?? **Authentication Flow Issues**

### **"Authorization code expired"**

**Error:**
```
Invalid or expired authorization code
```

**Cause:**
OAuth codes expire after ~60 seconds.

**Solution:**

**For Manual Testing:**
- Work faster! Copy code immediately
- Have API ready to receive it

**For Automated Flow:**
- Use the HTML test file (handles timing automatically)
- Let React handle the redirect (no manual copying)

**Increase Timeout (Dev Only):**
1. **Realm settings** ? **Tokens** tab
2. **Client Login Timeout:** Change from `1 minute` to `5 minutes`
3. **Don't use in production** (security risk)

---

### **"User has no tenants"**

**Symptom:**
User authenticated but sees "No access" page.

**Expected Behavior:**
This is correct if user has no tenant assignments.

**Solutions:**

**Option 1: Invite User**
1. Admin creates invitation
2. User accepts invitation
3. User assigned to tenant

**Option 2: Manually Assign**
```sql
INSERT INTO UserTenants (UserId, TenantId, IsAdmin)
VALUES (
  'keycloak-user-id',
  1,  -- tenant ID
  0   -- isAdmin (0 or 1)
);
```

**Option 3: Create New Organization**
- User clicks "Create Organization"
- New tenant created automatically

---

### **"Token is not active"**

**Error:**
```
Token is not active
Token is expired
```

**Cause:**
JWT token has expired.

**Solution:**

**Check Token Expiration:**
1. Copy token
2. Go to https://jwt.io
3. Paste token
4. Check `exp` claim (Unix timestamp)

**Get New Token:**
- User logs in again
- Token automatically refreshed

**Increase Token Lifespan (Dev Only):**
1. **Realm settings** ? **Tokens** tab
2. **Access Token Lifespan:** `15 minutes` ? `1 hour`
3. **Don't use in production**

---

## ?? **Database Issues**

### **User Not Syncing**

**Symptom:**
User exists in Keycloak but not in database.

**Causes:**
- API not calling user sync
- Database connection issue
- Validation errors

**Solution:**

**Force User Sync:**
1. User logs out
2. User logs in again
3. Auth callback should sync user

**Check Database Connection:**
```bash
dotnet ef database update
```

**Check Logs:**
```
Look for: "Syncing user to local database"
Should see: "Successfully synced user"
```

**Manual Verification:**
```sql
SELECT * FROM Users 
WHERE Email = 'user@example.com';
```

---

### **Invitation Not Accepted**

**Symptom:**
User accepted invitation but `IsAccepted` still `0`.

**Causes:**
- Wrong invitation token
- Token expired
- API error during acceptance

**Solution:**

**Check Invitation:**
```sql
SELECT * FROM TenantInvitations
WHERE Email = 'user@example.com';
```

**Check Expiration:**
```sql
SELECT *, 
  CASE WHEN ExpiresAt < GETUTCDATE() THEN 'Expired' ELSE 'Valid' END as Status
FROM TenantInvitations
WHERE Email = 'user@example.com';
```

**Check API Logs:**
```
Look for: "Processing invitation flow"
Should see: "successfully added to tenant via invitation"
```

**Manually Accept (Dev Only):**
```sql
UPDATE TenantInvitations
SET 
  IsAccepted = 1,
  AcceptedAt = GETUTCDATE(),
  AcceptedByUserId = 'user-id'
WHERE InvitationToken = 'token-here';
```

---

## ? **Common Errors**

### **Error Matrix**

| Error | Location | Common Cause | Quick Fix |
|-------|----------|--------------|-----------|
| Redirect URI mismatch | Keycloak | Wrong redirect URL | Add `http://localhost:5173/auth/callback*` |
| Invalid client credentials | API | Wrong secret | Copy secret from Keycloak ? `.env` |
| CORS error | Browser | Missing web origins | Add origins in Keycloak |
| Code expired | API | Slow manual testing | Use automated flow |
| No register button | Keycloak | Registration disabled | Enable in Realm settings |
| Cookie not set | React | Missing `credentials` | Add `credentials: 'include'` |
| User not synced | Database | Sync failed | Check logs, retry login |
| Token expired | API | Old token | User logs in again |

---

## ?? **Diagnostic Commands**

### **Check All Services**

```bash
# Keycloak
docker ps | grep keycloak
curl http://localhost:8080

# API
curl http://localhost:5123/swagger/index.html

# React
curl http://localhost:5173

# Database
dotnet ef database update
```

### **Check Configuration**

```bash
# Keycloak endpoints
curl http://localhost:8080/realms/groundup/.well-known/openid-configuration

# API health (if you have health endpoint)
curl http://localhost:5123/health
```

### **Check Logs**

```bash
# Keycloak logs
docker-compose -f keycloak-compose.yml logs -f keycloak

# API logs
# In terminal running dotnet run

# React logs
# In terminal running npm run dev
```

---

## ?? **Getting Help**

### **Information to Gather**

1. **Error Message:**
   - Exact error text
   - Where it appears (browser, API logs, etc.)

2. **Steps to Reproduce:**
   - What you did
   - What you expected
   - What actually happened

3. **Configuration:**
   - Keycloak version
   - .NET version
   - React version

4. **Logs:**
   - API logs
   - Keycloak logs
   - Browser console errors

### **Useful Log Messages**

**Successful Flow:**
```
[INFO] User abc123 authenticated via Keycloak
[INFO] Syncing user to local database
[INFO] Successfully synced user
[INFO] Processing invitation flow
[INFO] User successfully added to tenant
```

**Error Indicators:**
```
[ERROR] Failed to exchange authorization code
[ERROR] Failed to sync user to database
[WARNING] Failed to accept invitation
[ERROR] User not found in Keycloak
```

---

## ? **Troubleshooting Checklist**

- [ ] All services running (Keycloak, API, React)
- [ ] Keycloak accessible at port 8080
- [ ] API accessible at port 5123
- [ ] React accessible at port 5173
- [ ] Redirect URI configured: `http://localhost:5173/auth/callback*`
- [ ] Web origins configured
- [ ] Client authentication enabled
- [ ] Standard flow enabled
- [ ] Client secret matches `.env`
- [ ] `.env` file in correct location
- [ ] Database connection working
- [ ] User registration enabled in Keycloak
- [ ] Roles created (SYSTEMADMIN, Admin, Member)
- [ ] React uses `credentials: 'include'`
- [ ] No extra quotes/spaces in `.env`

---

**Updated:** 2025-01-21  
**Status:** ? Complete Troubleshooting Guide  
**Coverage:** Keycloak, API, React, Database, Common Errors
