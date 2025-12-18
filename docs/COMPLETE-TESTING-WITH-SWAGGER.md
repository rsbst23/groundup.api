# Complete Manual Testing Guide - Using Swagger UI

## ?? **Testing Made Easy with Swagger!**

Your API has Swagger UI, which makes testing without a frontend much simpler. You can test all endpoints directly from your browser.

---

## Prerequisites

### 1. Start Services

```bash
# Terminal 1: Start Keycloak
docker-compose -f keycloak-compose.yml up -d

# Terminal 2: Start API
cd GroundUp.api
dotnet run

# Verify Keycloak is running
curl http://localhost:8080

# Verify API is running
curl http://localhost:5000/swagger
```

### 2. Access Swagger UI

Open in browser:
```
http://localhost:5000/swagger
```

You should see the Swagger documentation with all your endpoints.

### 3. Configure Keycloak (One-Time Setup)

1. **Visit Keycloak Admin:** `http://localhost:8080`
2. **Login:** Username: `admin`, Password: `admin`
3. **Create Realm:** 
   - Click "Create Realm"
   - Name: `groundup`
   - Click "Create"
4. **Create Client:**
   - Go to Clients ? Create
   - Client ID: `groundup-api`
   - Client Protocol: `openid-connect`
   - Click "Save"
   - Settings:
     - Access Type: `confidential`
     - Valid Redirect URIs: `http://localhost:5000/*`
     - Web Origins: `http://localhost:5000`
   - Click "Save"
5. **Enable Self-Registration:**
   - Go to Realm Settings ? Login
   - Enable "User Registration"
   - Click "Save"

---

## ?? **Test Flow 1: Standard Self-Service Signup**

### Step 1: Initiate Standard Login

**In your browser (NOT Swagger):**
```
http://localhost:5000/api/auth/login/standard
```

**What happens:**
- Redirects to Keycloak
- Shows login screen with "Register" link

### Step 2: Register New User

- Click **"Register"**
- Fill out form:
  - **Email:** `john@example.com`
  - **First Name:** `John`
  - **Last Name:** `Doe`
  - **Username:** `john_doe`
  - **Password:** `Password123!`
  - **Confirm Password:** `Password123!`
- Click **"Register"**

### Step 3: Observe Auto-Redirect

Keycloak redirects to: `http://localhost:5000/api/auth/callback?code=...`

**Browser displays JSON:**
```json
{
  "success": true,
  "data": {
    "success": true,
    "flow": "new_org",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c",
    "tenantId": 1,
    "tenantName": "John's Organization",
    "requiresTenantSelection": false,
    "isNewOrganization": true,
    "message": "Organization created successfully"
  }
}
```

### Step 4: Copy Token and Test in Swagger

1. **Copy the token value** from the JSON response
2. **Go to Swagger UI:** `http://localhost:5000/swagger`
3. **Click "Authorize"** button (top right with lock icon)
4. **Paste token** in the "Value" field (it should auto-add "Bearer " prefix)
5. **Click "Authorize"**
6. **Click "Close"**

### Step 5: Test Authenticated Endpoint

**In Swagger:**
1. Find **`GET /api/auth/me`**
2. Click **"Try it out"**
3. Click **"Execute"**

**Expected Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "guid-here",
    "email": "john@example.com",
    "username": "john_doe",
    "fullName": "John Doe",
    "roles": []
  },
  "message": "User profile retrieved successfully",
  "statusCode": 200
}
```

### Step 6: Verify Tenant Created

**In Swagger:**
1. Find **`GET /api/tenants`**
2. Click **"Try it out"**
3. Set `pageNumber` = `1`, `pageSize` = `10`
4. Click **"Execute"**

**You should see:**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 1,
        "name": "John's Organization",
        "tenantType": 0,
        "realmName": "groundup",
        "isActive": true
      }
    ],
    "pageNumber": 1,
    "pageSize": 10,
    "totalRecords": 1,
    "totalPages": 1
  }
}
```

? **Success!** Standard user signup with auto-created tenant works!

---

## ?? **Test Flow 2: Enterprise Tenant Signup**

### Step 1: Create Enterprise Tenant (Swagger)

**In Swagger:**
1. Find **`POST /api/tenants/enterprise/signup`**
2. Click **"Try it out"**
3. **No authorization needed** (this is a public endpoint)
4. Edit the request body:
```json
{
  "companyName": "Acme Corporation",
  "contactEmail": "admin@acme.com",
  "contactName": "Jane Admin",
  "requestedSubdomain": "acme",
  "customDomain": "acme.yourapp.com",
  "plan": "enterprise-trial"
}
```
5. Click **"Execute"**

**Expected Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "tenantId": 2,
    "tenantName": "Acme Corporation",
    "realmName": "tenant_acme_xyz1",
    "customDomain": "acme.yourapp.com",
    "invitationToken": "abc123-def456-789xyz",
    "invitationUrl": "http://localhost:5000/accept-invitation?token=abc123-def456-789xyz",
    "emailSent": false,
    "message": "Enterprise tenant created. Invitation URL: ..."
  }
}
```

### Step 2: Copy Invitation Token

From the response, copy the `invitationToken` value (e.g., `abc123-def456-789xyz`)

### Step 3: Visit Enterprise Invitation URL

**In browser (NOT Swagger):**
```
http://localhost:5000/api/invitations/enterprise/invite/{invitationToken}
```

**Example:**
```
http://localhost:5000/api/invitations/enterprise/invite/abc123-def456-789xyz
```

**What happens:**
- Validates invitation
- Redirects to Keycloak **enterprise realm** (not shared "groundup" realm)
- Shows Keycloak login screen

### Step 4: Register in Enterprise Realm

- Click **"Register"**
- **?? IMPORTANT:** Email MUST match invitation email: `admin@acme.com`
- Fill out:
  - **Email:** `admin@acme.com`
  - **First Name:** `Jane`
  - **Last Name:** `Admin`
  - **Username:** `acme_admin`
  - **Password:** `Password123!`
- Click **"Register"**

### Step 5: Get Token from Callback

Browser redirects to callback and shows JSON with token.

**Copy the token.**

### Step 6: Test in Swagger

1. **Authorize in Swagger** with the new token
2. Test **`GET /api/auth/me`**

**Expected:**
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "email": "admin@acme.com",
    "username": "acme_admin",
    "fullName": "Jane Admin"
  }
}
```

### Step 7: Verify Enterprise Tenant

**In Swagger (still authenticated as Jane):**
1. **`GET /api/tenants`**

**You should see:**
```json
{
  "items": [
    {
      "id": 2,
      "name": "Acme Corporation",
      "tenantType": 1,
      "realmName": "tenant_acme_xyz1",
      "customDomain": "acme.yourapp.com"
    }
  ]
}
```

? **Success!** Enterprise signup with dedicated realm works!

---

## ?? **Test Flow 3: Admin Invites User to Tenant**

**Prerequisite:** You're still authenticated as John (from Flow 1).

### Step 1: Create Invitation (Swagger)

**In Swagger (authenticated as John):**
1. Find **`POST /api/tenant-invitations`**
2. Click **"Try it out"**
3. Request body:
```json
{
  "contactEmail": "alice@example.com",
  "isAdmin": false
}
```
4. Click **"Execute"**

**Expected Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": "invitation-guid",
    "invitationToken": "xyz789-abc123",
    "contactEmail": "alice@example.com",
    "tenantName": "John's Organization",
    "status": "Pending",
    "expiresAt": "2025-01-05T00:00:00Z"
  }
}
```

### Step 2: Copy Invitation Token

From response: `xyz789-abc123`

### Step 3: Visit Invitation URL

**In browser:**
```
http://localhost:5000/api/invitations/invite/xyz789-abc123
```

**What happens:**
- Validates invitation
- Redirects to Keycloak **shared realm** (`groundup`)
- Shows registration

### Step 4: Register as Alice

- Email: `alice@example.com` (must match invitation)
- Username: `alice`
- Password: `Password123!`
- Complete registration

### Step 5: Get Alice's Token

After redirect, copy token from JSON response.

### Step 6: Test Alice in Swagger

1. **Authorize with Alice's token**
2. **`GET /api/auth/me`** ? Should show Alice's profile
3. **`GET /api/tenants`** ? Should show "John's Organization"

? **Success!** Invitation flow works!

---

## ?? **Test Flow 4: Join Link (Open Registration)**

**Prerequisite:** Authenticated as John (tenant admin).

### Step 1: Create Join Link (Swagger)

**In Swagger (as John):**
1. Find **`POST /api/tenant-join-links`**
2. Click **"Try it out"**
3. Request body:
```json
{
  "expirationDays": 30
}
```
4. Click **"Execute"**

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "tenantId": 1,
    "joinToken": "join123-xyz789",
    "joinUrl": "http://localhost:5000/api/join/join123-xyz789",
    "expiresAt": "2025-02-15T00:00:00Z",
    "isRevoked": false
  }
}
```

### Step 2: Copy Join URL

From response: `http://localhost:5000/api/join/join123-xyz789`

### Step 3: Visit Join Link (New User)

**In browser (incognito/private mode or logout from Keycloak first):**
```
http://localhost:5000/api/join/join123-xyz789
```

**What happens:**
- Validates join link
- Redirects to Keycloak shared realm
- Shows registration

### Step 4: Register New User

- Email: `bob@example.com`
- Username: `bob`
- Password: `Password123!`
- Complete registration

### Step 5: Test Bob

Copy token, authorize in Swagger, test endpoints.

? **Success!** Join link flow works!

---

## ?? **Test Flow 5: List and Revoke Join Links**

**In Swagger (as John):**

### List Join Links

1. **`GET /api/tenant-join-links`**
2. Parameters: `pageNumber=1`, `pageSize=10`, `includeRevoked=false`
3. Click **"Execute"**

**Expected:**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 1,
        "joinToken": "join123-xyz789",
        "joinUrl": "http://localhost:5000/api/join/join123-xyz789",
        "expiresAt": "2025-02-15T00:00:00Z",
        "isRevoked": false
      }
    ]
  }
}
```

### Revoke Join Link

1. **`DELETE /api/tenant-join-links/{id}`**
2. Parameter: `id = 1`
3. Click **"Execute"**

**Expected:**
```json
{
  "success": true,
  "data": true,
  "message": "Join link revoked successfully"
}
```

### Verify Revoked

Try visiting the join URL again - should fail with "Join link has been revoked".

? **Success!** Join link management works!

---

## ?? **Test Flow 6: Enterprise User Login**

### Step 1: Login to Existing Enterprise Realm

**In browser:**
```
http://localhost:5000/api/auth/login/enterprise?realm=tenant_acme_xyz1
```

**Note:** Use the actual realm name from your enterprise signup response.

### Step 2: Login with Jane's Credentials

- Username: `acme_admin`
- Password: `Password123!`

### Step 3: Get Token

Copy token from callback JSON response.

### Step 4: Test in Swagger

Authorize with token, test endpoints.

? **Success!** Enterprise login works!

---

## ?? **Complete Testing Checklist**

| # | Flow | Method | Status |
|---|------|--------|--------|
| 1 | Standard Signup | Browser ? Keycloak | ? |
| 2 | Enterprise Signup | Swagger POST | ? |
| 3 | Enterprise Admin | Browser ? Invitation URL | ? |
| 4 | Standard Invitation | Swagger POST ? Browser | ? |
| 5 | Join Link Create | Swagger POST | ? |
| 6 | Join Link Use | Browser ? Join URL | ? |
| 7 | Join Link List | Swagger GET | ? |
| 8 | Join Link Revoke | Swagger DELETE | ? |
| 9 | Enterprise Login | Browser ? Login URL | ? |
| 10 | Token Validation | Swagger with Auth | ? |

---

## ?? **Database Verification Queries**

After testing, verify data in your database:

```sql
-- 1. Check all users
SELECT Id, Email, Username, FirstName, LastName, IsActive 
FROM Users;

-- 2. Check all tenants
SELECT Id, Name, TenantType, RealmName, CustomDomain 
FROM Tenants;

-- 3. Check user-tenant mappings (with ExternalUserId)
SELECT 
    ut.Id,
    u.Email as UserEmail,
    t.Name as TenantName,
    ut.ExternalUserId,
    ut.IsAdmin,
    ut.JoinedAt
FROM UserTenants ut
JOIN Users u ON ut.UserId = u.Id
JOIN Tenants t ON ut.TenantId = t.Id
ORDER BY ut.JoinedAt DESC;

-- 4. Check invitations
SELECT 
    ti.Id,
    ti.ContactEmail,
    t.Name as TenantName,
    ti.Status,
    ti.IsAdmin,
    ti.CreatedAt,
    ti.AcceptedAt
FROM TenantInvitations ti
JOIN Tenants t ON ti.TenantId = t.Id
ORDER BY ti.CreatedAt DESC;

-- 5. Check join links
SELECT 
    jl.Id,
    t.Name as TenantName,
    jl.JoinToken,
    jl.IsRevoked,
    jl.ExpiresAt,
    jl.CreatedAt
FROM TenantJoinLinks jl
JOIN Tenants t ON jl.TenantId = t.Id
ORDER BY jl.CreatedAt DESC;
```

---

## ?? **Expected Results**

After completing all tests, you should have:

### Users:
- ? `john@example.com` (Standard user, admin of "John's Organization")
- ? `admin@acme.com` (Enterprise user, admin of "Acme Corporation")
- ? `alice@example.com` (Standard user, member of "John's Organization")
- ? `bob@example.com` (Standard user, member of "John's Organization" via join link)

### Tenants:
- ? **Tenant 1:** "John's Organization" (Standard, realm: `groundup`)
- ? **Tenant 2:** "Acme Corporation" (Enterprise, realm: `tenant_acme_xyz1`)

### UserTenant Mappings:
- ? All mappings have `ExternalUserId` populated (Keycloak sub claim)
- ? John and admin@acme.com have `IsAdmin = true`
- ? Alice and Bob have `IsAdmin = false`

### Keycloak Realms:
- ? **`groundup`** realm: Has john, alice, bob
- ? **`tenant_acme_xyz1`** realm: Has admin@acme.com

---

## ?? **All Flows Working!**

If all tests pass, your authentication system is **fully operational** and ready for frontend integration!

### What's Working:
1. ? Standard self-service signup with auto-created tenant
2. ? Enterprise tenant creation with dedicated Keycloak realm
3. ? Enterprise admin invitation flow
4. ? Standard user invitations
5. ? Join link creation and usage
6. ? Join link management (list, revoke)
7. ? Enterprise user login
8. ? Standard user login
9. ? Auth callback handling all flows
10. ? Token-based authentication

### What's Next:
- **Frontend Integration:** Build React UI to consume these APIs
- **Email Service:** Add AWS SES or SMTP for invitation emails (Phase 4)
- **Permission System:** Enhance role-based access control

---

## ?? **Tips for Testing**

1. **Use Incognito/Private Mode** when testing multiple users to avoid Keycloak session conflicts
2. **Keep Swagger open** - it's your best friend for API testing
3. **Copy tokens to a text file** - you'll need them for subsequent tests
4. **Check database after each flow** - verify data integrity
5. **Use unique emails** - makes debugging easier
6. **Check API logs** - `dotnet run` output shows detailed flow information

---

## ? **Troubleshooting**

### Issue: "Redirect URI mismatch"
**Solution:** In Keycloak client settings, ensure:
- Valid Redirect URIs: `http://localhost:5000/*`
- Web Origins: `http://localhost:5000`

### Issue: "Realm not found"
**Solution:** 
- For standard flows: Ensure "groundup" realm exists
- For enterprise flows: Check the realm name in enterprise signup response

### Issue: "Token expired"
**Solution:** Tokens expire after 1 hour. Re-login to get a fresh token.

### Issue: "User not found in Keycloak"
**Solution:** User must complete registration in Keycloak before backend creates User record.

### Issue: "Invitation already accepted"
**Solution:** Each invitation can only be used once. Create a new invitation.

---

**Happy Testing!** ??
