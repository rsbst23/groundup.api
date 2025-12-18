# 🧪 Manual Testing Guide: Enterprise Tenant Flow (No Frontend)

This guide walks you through testing the complete enterprise tenant provisioning flow using **Swagger**, **cURL**, and **Keycloak UI** - no frontend required!

---

## 📋 **Prerequisites**

- ✅ Keycloak running at `http://localhost:8080`
- ✅ GroundUp API running at `http://localhost:5123`
- ✅ MySQL database configured
- ✅ Swagger UI accessible at `http://localhost:5123/swagger`
- ✅ SMTP configured (for email verification) - see `.env` file
  - AWS SES credentials in `SMTP_USERNAME` and `SMTP_PASSWORD`
  - Sender email verified in AWS SES Console
  - See `docs/AWS-SES-QUICK-START.md` for setup instructions

---

## ⚠️ **Important: Email Verification**

**Email verification behavior depends on your environment:**

### **Development Environment (ASPNETCORE_ENVIRONMENT=Development)**
- ✅ Email verification is **DISABLED** by default
- Users can register and login immediately without verifying email
- Makes testing much easier - no need to check email or click verification links
- **This is the recommended setting for local testing**

### **Production Environment**
- Email verification is **ENABLED** if SMTP is configured
- Users must verify their email before logging in
- Provides better security and confirms user email addresses

### **Current Setup:**

Your environment is set to **Development** mode, so:
- ❌ Email verification is **DISABLED** for new enterprise realms
- ✅ You can register and login immediately
- ✅ No need to click email verification links

**To test with email verification enabled:**
1. Set `ASPNETCORE_ENVIRONMENT=Production` in your `.env` file
2. Ensure SMTP is configured (AWS SES credentials)
3. Restart the API
4. Create a new enterprise tenant
5. Email verification will be required for new registrations

---

## **Step 1: Create Enterprise Tenant**

### **Via Swagger:**

1. Open Swagger: `http://localhost:5123/swagger`
2. Find `POST /api/tenants/enterprise/signup`
3. Click "Try it out"
4. Enter request body:

```json
{
  "companyName": "Acme Corporation",
  "contactEmail": "rsbst23@yahoo.com",
  "contactName": "Jane Doe",
  "customDomain": "https://www.acmecorp.com",
  "requestedSubdomain": "acme",
  "plan": "enterprise-trial"
}
```

5. Click "Execute"

### **Via cURL:**

```bash
curl -X POST "http://localhost:5123/api/tenants/enterprise/signup" \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Acme Corporation",
    "contactEmail": "rsbst23@yahoo.com",
    "contactName": "Jane Doe",
    "customDomain": "https://www.acmecorp.com",
    "requestedSubdomain": "acme",
    "plan": "enterprise-trial"
  }'
```

### **Expected Response:**

```json
{
  "success": true,
  "data": {
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "realmName": "tenant_acme_a3f2",
    "customDomain": "https://www.acmecorp.com",
    "invitationToken": "abc123def456...",
    "invitationUrl": "http://localhost:5123/accept-invitation?token=abc123def456...",
    "emailSent": true,
    "message": "Enterprise tenant created. Invitation email sent to rsbst23@yahoo.com"
  }
}
```

**📝 Save these values:**
- ✅ `realmName`: `tenant_acme_a3f2`
- ✅ `invitationToken`: `abc123def456...`

**📧 Check your email:**
- You should receive an invitation email at `rsbst23@yahoo.com`
- The email will have a link to accept the invitation and set up your account

---

## **Step 2: Verify Realm Created in Keycloak**

1. Open Keycloak Admin: `http://localhost:8080/admin`
2. Login as admin (default: `admin` / `admin`)
3. Click the realm dropdown (top-left)
4. **You should see:** `tenant_acme_a3f2` in the list
5. Select that realm
6. Verify settings:
   - **Login Tab:**
     - User registration: ✅ ON
     - Email as username: ❌ OFF
     - Verify email: ❌ OFF (disabled for testing - no SMTP configured)
     - Forgot password: ✅ ON
   - **Email Tab:**
     - Should be empty (no SMTP configured)

---

## **Step 3: Verify Database Records**

Run these SQL queries against your MySQL database:

### **Check Tenant Created:**

```sql
SELECT * FROM Tenants WHERE TenantType = 'enterprise';
```

**Expected result:**
```
Id: 1
Name: "Acme Corporation"
TenantType: "enterprise"
KeycloakRealm: "tenant_acme_a3f2"
CustomDomain: "acme.yourapp.com"
Plan: "enterprise-trial"
IsActive: true
```

### **Check Invitation Created:**

```sql
SELECT * FROM TenantInvitations WHERE TenantId = 1;
```

**Expected result:**
```
InvitationToken: "abc123def456..."
ContactEmail: "admin@acme.com"
ContactName: "Jane Doe"
IsAdmin: true
IsAccepted: false
ExpiresAt: (7 days from now)
CreatedByUserId: NULL
```

---

## **Step 4: Get Invitation Details (Simulate Frontend)**

This simulates what the frontend would do when a user clicks the invitation link.

### **Via Swagger:**

1. Find `GET /api/invitations/token/{token}`
2. Enter path parameter: `token = abc123def456...` (from Step 1)
3. Click "Execute"

### **Via cURL:**

```bash
curl "http://localhost:5123/api/invitations/token/abc123def456..."
```

### **Expected Response:**

```json
{
  "success": true,
  "data": {
    "id": 1,
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "contactEmail": "rsbst23@yahoo.com",
    "contactName": "Jane Doe",
    "isAdmin": true,
    "isExpired": false,
    "isAccepted": false,
    "expiresAt": "2025-12-08T..."
  }
}
```

**Confirm:**
- ✅ `tenantId`: `1`
- ✅ `tenantName`: `"Acme Corporation"`
- ✅ `isExpired`: `false`
- ✅ `isAccepted`: `false`

---

## **Step 5: Create First Admin Account in Keycloak**

**🎉 Simplified Testing**: In development mode, email verification is disabled, making testing much easier!

### **Recommended: Via OAuth Registration Flow**

1. First, build the state parameter for the invitation flow:

```javascript
// In browser console
const state = {
  flow: "invitation",
  invitationToken: "YOUR_TOKEN_FROM_STEP_1", // Replace with your actual token
  realm: "YOUR_REALM_FROM_STEP_1" // Replace with your actual realm
};

const stateEncoded = btoa(JSON.stringify(state));
console.log("Your registration URL state:", stateEncoded);
```

2. Build the registration URL with OAuth parameters:

```
http://localhost:8080/realms/{YOUR_REALM}/protocol/openid-connect/registrations?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state={YOUR_BASE64_STATE}
```

**Example (replace placeholders):**
```
http://localhost:8080/realms/tenant_acme_aa8f/protocol/openid-connect/registrations?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=eyJmbG93IjoiaW52aXRhdGlvbiIsImludml0YXRpb25Ub2tlbiI6IjY3NDNlYjM2ZGJhMDQ1OTg5ODBiMzY0NmJjMTM3YmE2IiwicmVhbG0iOiJ0ZW5hbnRfYWNtZV9hYThmIn0=
```

3. Open this URL in your browser

4. Fill out the registration form:
   - **Username:** `jane.doe`
   - **Email:** `rsbst23@yahoo.com` (must match the invitation email)
   - **First Name:** `Jane`
   - **Last Name:** `Doe`
   - **Password:** `SecurePassword123!`
   - **Confirm Password:** `SecurePassword123!`

5. Click "Register"

6. **In development mode:**
   - ✅ **No email verification required!**
   - You'll be immediately redirected to: `http://localhost:5123/api/auth/callback?code=...&state=...`
   - The API will exchange the code, create your user, accept the invitation, and assign you to the tenant
   - You should see a JSON response with your authentication token (see Step 7 for expected response)

7. **In production mode (if enabled):**
   - Check your email for the verification link
   - Click the verification link
   - ⚠️ **Note:** You'll need to manually initiate login after verification (see Step 6)

### **Alternative: Via Keycloak Admin Console**

Use this method if you want to bypass the OAuth flow for testing:

1. Go to Keycloak Admin: `http://localhost:8080/admin`
2. Select realm: Your enterprise realm from Step 1
3. Go to: **Users** (left sidebar)
4. Click: **Add user**
5. Fill in:
   - Username: `jane.doe`
   - Email: `rsbst23@yahoo.com`
   - First Name: `Jane`
   - Last Name: `Doe`
   - Email Verified: ✅ ON (optional - not required in dev mode)
6. Click "Create"
7. Go to **Credentials** tab
8. Click "Set password"
9. Set password: `SecurePassword123!`
10. Temporary: ❌ OFF
11. Click "Save"
12. **Now you must manually initiate the login flow** (go to Step 6)

---

## **Step 6: Test Full Login Flow (Optional - Only if you used Admin Console)**

⚠️ **Skip this step if you registered via the OAuth registration flow in Step 5** - you should have already been redirected to the callback and received your auth token!

This step is only needed if you created the user via the Keycloak Admin Console and skipped the OAuth flow.

**Prerequisites:**
- ✅ User account created via Keycloak Admin Console (Step 5, Alternative method)
- ✅ Invitation token from Step 1

### **Build the Login URL with State:**

First, create the state parameter (base64-encoded JSON):

```javascript
// In browser console or use https://www.base64encode.org/
const state = {
  flow: "invitation",
  invitationToken: "abc123def456...", // From Step 1 - your actual token
  realm: "tenant_acme_a3f2" // From Step 1 - your actual realm
};

const stateEncoded = btoa(JSON.stringify(state));
console.log(stateEncoded);
```

**Example result:**
```
eyJmbG93IjoiaW52aXRhdGlvbiIsImludml0YXRpb25Ub2tlbiI6ImFiYzEyM2RlZjQ1Ni4uLiIsInJlYWxtIjoidGVuYW50X2FjbWVfYTNmMiJ9
```

### **Full Login URL:**

Replace the placeholders with your actual values:

```
http://localhost:8080/realms/{YOUR_REALM}/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state={YOUR_BASE64_STATE}
```

**Example (fill in your actual realm and state):**
```
http://localhost:8080/realms/tenant_acme_a3f2/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=eyJmbG93IjoiaW52aXRhdGlvbiIsImludml0YXRpb25Ub2tlbiI6ImFiYzEyM2RlZjQ1Ni4uLiIsInJlYWxtIjoidGVuYW50X2FjbWVfYTNmMiJ9
```

### **Test It:**

1. **Paste the complete URL in your browser** (make sure you've replaced the placeholders)
2. Login with:
   - Username: `jane.doe`
   - Password: `SecurePassword123!`
3. **After login, Keycloak redirects to:**
```
http://localhost:5123/api/auth/callback?code=...&state=...
```

4. **Your API processes the callback and:**
   - Extracts the authorization code
   - Exchanges code for tokens (using the realm from state)
   - Parses state (finds invitation token)
   - Calls `HandleInvitationFlowAsync`
   - Creates User record (if first login)
   - Creates UserKeycloakIdentity mapping
   - Accepts invitation
   - Assigns to tenant as admin
   - Issues JWT token
   - Returns success response

5. **You should see a JSON response in your browser like:**

```json
{
  "success": true,
  "data": {
    "flow": "invitation",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "success": true,
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "requiresTenantSelection": false,
    "message": "Invitation accepted successfully"
  }
}
```

---

## **Step 7: Verify Everything Worked**

### **Check API Response:**

You should see a JSON response like:

```json
{
  "success": true,
  "data": {
    "flow": "invitation",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "requiresTenantSelection": false,
    "message": "Invitation accepted successfully"
  }
}
```

### **Verify Database Changes:**

```sql
-- Check user created
SELECT * FROM Users;
-- Expected: New user record with email "admin@acme.com"

-- Check identity mapping created
SELECT * FROM UserKeycloakIdentities;
-- Expected: Mapping with RealmName = "tenant_acme_a3f2"

-- Check invitation accepted
SELECT * FROM TenantInvitations WHERE InvitationToken = 'abc123def456...';
-- Expected: IsAccepted = true, AcceptedAt populated

-- Check user assigned to tenant
SELECT * FROM UserTenants WHERE TenantId = 1;
-- Expected: User linked to tenant as admin (IsAdmin = true)
```

---

## **Step 8: Test Subsequent Login**

Test that the user can log in again normally (without invitation token).

### **Build Normal Login URL:**

```javascript
const state = {
  flow: "default",
  realm: "tenant_acme_a3f2" // Use your actual realm from Step 1
};

const stateEncoded = btoa(JSON.stringify(state));
console.log(stateEncoded);
```

### **Login URL:**

Replace the placeholders with your actual values:

```
http://localhost:8080/realms/{YOUR_REALM}/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state={YOUR_BASE64_STATE}
```

**Example:**
```
http://localhost:8080/realms/tenant_acme_a3f2/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=eyJmbG93IjoiZGVmYXVsdCIsInJlYWxtIjoidGVuYW50X2FjbWVfYTNmMiJ9
```

### **Test It:**

1. Paste URL in browser
2. Login as `jane.doe`
3. Should redirect to callback
4. **Expected response:**

```json
{
  "success": true,
  "data": {
    "flow": "default",
    "success": true,
    "token": "...",
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "requiresTenantSelection": false
  }
}
```

---

## **Step 9: Test Realm Resolution**

Test the realm lookup by subdomain (for future subdomain support).

### **Via Swagger:**

1. Find `POST /api/tenants/resolve-realm`
2. Enter request body:

```json
{
  "url": "acme"
}
```
3. Click "Execute"

### **Via cURL:**

```bash
curl -X POST "http://localhost:5123/api/tenants/resolve-realm" \
  -H "Content-Type: application/json" \
  -d '{"url": "acme"}'
```

### **Expected Response:**

```json
{
  "success": true,
  "data": {
    "realm": "tenant_acme_a3f2",
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "isEnterprise": true
  }
}
```

**This proves realm resolution works!** ✅

---

## 📋 **Complete Testing Checklist**

- [ ] Enterprise signup creates tenant ✅
- [ ] Keycloak realm created ✅
- [ ] Database records correct (Tenant, TenantInvitation) ✅
- [ ] Invitation details retrieved by token ✅
- [ ] User registration in enterprise realm works ✅
- [ ] First login accepts invitation ✅
- [ ] User record created ✅
- [ ] Identity mapping created ✅
- [ ] User assigned to tenant as admin ✅
- [ ] Subsequent login works ✅
- [ ] Realm resolution works ✅

---

## 🔧 **Quick Test Script**

Save this as `test-enterprise-flow.sh`:

```bash
#!/bin/bash

echo "🚀 Testing Enterprise Tenant Flow"
echo ""

# Step 1: Create enterprise tenant
echo "📝 Step 1: Creating enterprise tenant..."
RESPONSE=$(curl -s -X POST "http://localhost:5123/api/tenants/enterprise/signup" \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Test Corp",
    "contactEmail": "admin@test.com",
    "contactName": "Test Admin",
    "customDomain": "https://test.example.com",
    "requestedSubdomain": "test",
    "plan": "enterprise-trial"
  }')

echo $RESPONSE | jq '.')

REALM=$(echo $RESPONSE | jq -r '.data.realmName')
TOKEN=$(echo $RESPONSE | jq -r '.data.invitationToken')

echo ""
echo "✅ Realm created: $REALM"
echo "✅ Invitation token: $TOKEN"
echo ""

# Step 2: Get invitation details
echo "📝 Step 2: Getting invitation details..."
curl -s "http://localhost:5123/api/invitations/token/$TOKEN" | jq '.'

echo ""
echo "🎉 Enterprise tenant created successfully!"
echo ""
echo "Next steps:"
echo "1. Check your email for the verification link"
echo "2. Click the verification link to verify your email"
echo "3. Go to: http://localhost:8080/realms/$REALM/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=<BASE64_ENCODED_STATE>"
echo "4. Login with your credentials"
echo "5. Check the database for User, UserKeycloakIdentity, and UserTenant records"
```

Make it executable and run:

```bash
chmod +x test-enterprise-flow.sh
./test-enterprise-flow.sh
```

---

## 🐛 **Troubleshooting**

| Issue | Solution |
|-------|----------|
| **Forbidden error creating realm** | Make sure `admin-cli` has `create-realm` role assigned in Keycloak |
| **MySQL execution strategy error** | Fixed by wrapping transaction in `CreateExecutionStrategy()` |
| **FK constraint on CreatedByUserId** | Fixed by making `CreatedByUserId` nullable |
| **Realm not found** | Check Keycloak Admin Console - realm should be visible in dropdown |
| **User not created** | Check logs for errors during `HandleInvitationFlowAsync` |
| **Invalid redirect_uri error** | ✅ **FIXED** - `groundup-api` client is now automatically created with proper redirect URIs when realm is created |
| **"Token exchange failed" after registration** | ✅ **FIXED** - Email verification is now disabled in development mode. Delete the old realm and create a new enterprise tenant. The new realm will not require email verification. |
| **Email verification required in development** | ✅ **FIXED** - Email verification is automatically disabled when `ASPNETCORE_ENVIRONMENT=Development`. Restart the API and create a new enterprise tenant. |
| **Wrong API URL in redirect** | The API runs on `http://localhost:5123`, not `http://localhost:5000`. Update all URLs in your testing. |
| **State parameter decode error** | Make sure you're base64-encoding the JSON properly. Use `btoa(JSON.stringify(state))` in browser console or https://www.base64encode.org/ |
| **Still seeing email verification in new realm** | Make sure you've restarted the API after making environment changes. Delete the old realm in Keycloak Admin Console and create a fresh enterprise tenant. |

### **Understanding the Email Verification Flow**

**When you register via the OAuth registration URL (recommended):**

1. Browser opens registration URL with OAuth parameters (client_id, redirect_uri, state, etc.)
2. User fills out form and submits
3. Keycloak sends verification email
4. Keycloak shows a message: "You need to verify your email address"
5. User clicks verification link in email
6. Keycloak validates the token and marks email as verified
7. **Keycloak automatically continues the OAuth flow** that was initiated in step 1
8. Keycloak redirects to: `http://localhost:5123/api/auth/callback?code=...&state=...`
9. API exchanges code for tokens, creates user, accepts invitation, assigns to tenant
10. Success!

**When you create the user via Keycloak Admin Console:**

1. Admin creates user manually
2. Admin sets email as verified (checkbox)
3. Admin sets password
4. **No OAuth flow is initiated**
5. User must manually initiate login by going to the login URL (Step 6)

**Why the registration URL is recommended:**

- ✅ Automatic OAuth flow continuation
- ✅ Email verification integrated into the flow
- ✅ Single-step process (register → verify → callback → done)
- ✅ Better user experience (no manual login required)
- ✅ Maintains the invitation context throughout

**When to use the admin console method:**

- For testing without email verification
- When email delivery is not working
- For quick manual user creation
- When you want to skip the OAuth flow for debugging
