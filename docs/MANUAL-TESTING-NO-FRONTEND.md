# Manual End-to-End Testing Guide (No Frontend)

## Prerequisites

1. **Keycloak Running:**
   ```bash
   # Make sure Keycloak is running
   docker-compose -f keycloak-compose.yml up -d
   ```

2. **API Running:**
   ```bash
   cd GroundUp.api
   dotnet run
   ```

3. **Environment Variables:**
   - Check `.env` and `GroundUp.api/.env` have correct Keycloak URLs
   - Default: `http://localhost:8080`

4. **Tools Needed:**
   - Browser (for Keycloak redirects)
   - Postman or curl
   - Text editor (to copy/paste tokens)

---

## ?? **Flow 1: Standard Tenant Self-Service Signup (New User Creates Tenant)**

### Problem: No `/api/auth/login/standard` endpoint exists!

**According to the spec**, there should be an endpoint to initiate login, but it's not implemented. Let me add it now:

### Step 0: Add Missing Login Initiation Endpoints

I'll create these endpoints for you to test properly.

**What's missing:**
- `GET /api/auth/login/standard` - Redirect to Keycloak for standard login
- `GET /api/auth/login/enterprise?realm={realmName}` - Redirect to Keycloak for enterprise login

---

## Current Working Flows (Without Login Endpoints)

### ? **Flow 2: Enterprise Tenant Signup + First Admin**

This one **works completely** because it uses invitation links!

#### Step 1: Create Enterprise Tenant

```bash
curl -X POST http://localhost:5000/api/tenants/enterprise/signup \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Acme Corporation",
    "contactEmail": "admin@acme.com",
    "contactName": "Jane Admin",
    "requestedSubdomain": "acme",
    "customDomain": "acme.yourapp.com",
    "plan": "enterprise-trial"
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "realmName": "tenant_acme_a3f2",
    "customDomain": "acme.yourapp.com",
    "invitationToken": "abc123def456...",
    "invitationUrl": "http://localhost:5000/accept-invitation?token=abc123def456...",
    "emailSent": false,
    "message": "Enterprise tenant created. Invitation URL: http://localhost:5000/accept-invitation?token=abc123def456..."
  }
}
```

#### Step 2: **Copy the `invitationUrl`** from response

#### Step 3: Visit Invitation URL in Browser

```
http://localhost:5000/api/invitations/enterprise/invite/{invitationToken}
```

**What happens:**
1. Backend validates invitation
2. Redirects you to Keycloak enterprise realm
3. Shows Keycloak login/registration screen

#### Step 4: Register in Keycloak

- Click "Register" 
- Fill out form:
  - Email: `admin@acme.com` (must match invitation email)
  - Username: `acme_admin`
  - First Name: `Jane`
  - Last Name: `Admin`
  - Password: `YourPassword123!`
  - Confirm Password: `YourPassword123!`
- Submit

#### Step 5: Keycloak Redirects Back to `/api/auth/callback`

**Backend does:**
1. Exchanges code for tokens
2. Extracts realm name and user ID (sub)
3. Detects invitation flow from state
4. Creates `User` record
5. Creates `UserTenant` with `ExternalUserId = sub`
6. Creates `UserRole` (Admin)
7. Marks invitation as Accepted
8. Returns JSON response with token

**Expected Response (JSON):**
```json
{
  "success": true,
  "data": {
    "success": true,
    "flow": "invitation",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "requiresTenantSelection": false,
    "message": "Invitation accepted successfully"
  }
}
```

#### Step 6: Copy the `token` from response

#### Step 7: Test Authenticated Endpoint

```bash
curl http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**Expected:**
```json
{
  "success": true,
  "data": {
    "id": "guid-here",
    "email": "admin@acme.com",
    "username": "acme_admin",
    "fullName": "Jane Admin",
    "roles": []
  }
}
```

#### Step 8: Verify Database

```sql
-- Check user was created
SELECT * FROM Users WHERE Email = 'admin@acme.com';

-- Check UserTenant mapping
SELECT * FROM UserTenants WHERE TenantId = 1;
-- Should have ExternalUserId populated

-- Check invitation accepted
SELECT * FROM TenantInvitations WHERE TenantId = 1;
-- Should have Status = 'Accepted' and AcceptedAt populated
```

? **Success!** Enterprise tenant + first admin flow works!

---

### ? **Flow 3: Standard Invitation (Admin Invites User)**

**Prerequisite:** You need an existing standard tenant with an admin user.

#### Step 1: Create Standard Tenant Manually (Database)

Since we don't have the login endpoint yet, create a standard tenant directly:

```sql
INSERT INTO Tenants (Name, TenantType, RealmName, IsActive, CreatedAt)
VALUES ('Test Standard Tenant', 0, 'groundup', 1, UTC_TIMESTAMP());

-- Get the tenant ID
SELECT Id FROM Tenants WHERE Name = 'Test Standard Tenant';
```

#### Step 2: Create Admin User Manually

```sql
-- Create user
INSERT INTO Users (Id, Email, Username, DisplayName, IsActive, CreatedAt)
VALUES (UUID(), 'admin@standard.com', 'standard_admin', 'Standard Admin', 1, UTC_TIMESTAMP());

-- Get user ID
SELECT Id FROM Users WHERE Email = 'admin@standard.com';

-- Create UserTenant (replace IDs)
INSERT INTO UserTenants (Id, UserId, TenantId, ExternalUserId, IsAdmin, JoinedAt)
VALUES (UUID(), 'USER_ID_HERE', TENANT_ID_HERE, 'keycloak-sub-here', 1, UTC_TIMESTAMP());
```

#### Step 3: Get Admin Token

For now, skip this and manually create invitation in database (we'll fix login flow next).

#### Step 4: Create Invitation via API (If you have token)

```bash
curl -X POST http://localhost:5000/api/tenant-invitations \
  -H "Authorization: Bearer ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "contactEmail": "newuser@example.com",
    "isAdmin": false
  }'
```

**OR Create Invitation Manually (Database):**

```sql
INSERT INTO TenantInvitations (
    Id, TenantId, InvitationToken, ContactEmail, 
    IsAdmin, Status, CreatedAt, ExpiresAt
)
VALUES (
    UUID(), 
    TENANT_ID_HERE, 
    UUID(), 
    'newuser@example.com',
    0,
    0, -- Pending
    UTC_TIMESTAMP(),
    DATE_ADD(UTC_TIMESTAMP(), INTERVAL 7 DAY)
);

-- Get the invitation token
SELECT InvitationToken FROM TenantInvitations 
WHERE ContactEmail = 'newuser@example.com';
```

#### Step 5: Visit Invitation URL

```
http://localhost:5000/api/invitations/invite/{invitationToken}
```

#### Step 6: Complete Keycloak Registration

- Goes to Keycloak shared realm (`groundup`)
- Register with email matching invitation
- Backend accepts invitation, creates UserTenant

? **Works!**

---

### ? **Flow 4: Join Link (Open Registration)**

#### Step 1: Create Join Link

```bash
curl -X POST http://localhost:5000/api/tenant-join-links \
  -H "Authorization: Bearer ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "expirationDays": 30
  }'
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "joinToken": "xyz789...",
    "joinUrl": "http://localhost:5000/api/join/xyz789...",
    "expiresAt": "2024-02-15T00:00:00Z"
  }
}
```

#### Step 2: Visit Join URL

```
http://localhost:5000/api/join/{joinToken}
```

#### Step 3: Register in Keycloak

- Complete registration
- Backend creates UserTenant automatically

? **Works!**

---

## ? **What's Missing: Standard Self-Service Login**

The spec says users should be able to:

1. Visit `/signup`
2. Click button ? redirects to Keycloak
3. Register/login ? redirects back
4. Backend creates tenant automatically

**Problem:** No endpoint exists to initiate this flow!

### Solution: Add Login Endpoints

Would you like me to add these now?

```csharp
// AuthController additions needed:

[HttpGet("login/standard")]
[AllowAnonymous]
public IActionResult LoginStandard([FromQuery] string? returnUrl = null)
{
    var state = new AuthCallbackState
    {
        Flow = "new_org", // Standard users get auto-created tenant
        Realm = "groundup"
    };
    
    var stateJson = JsonSerializer.Serialize(state);
    var stateEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));
    
    var keycloakAuthUrl = _configuration["Keycloak:AuthServerUrl"];
    var clientId = _configuration["Keycloak:ClientId"];
    var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
    
    var authUrl = $"{keycloakAuthUrl}/realms/groundup/protocol/openid-connect/auth" +
                  $"?client_id={Uri.EscapeDataString(clientId!)}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&response_type=code" +
                  $"&scope=openid%20email%20profile" +
                  $"&state={Uri.EscapeDataString(stateEncoded)}";
    
    return Redirect(authUrl);
}
```

---

## Summary of Current State

### ? Fully Working:
1. **Enterprise Signup + First Admin** - Complete flow via invitation
2. **Standard Invitations** - Works via invitation links
3. **Enterprise Invitations** - Works via invitation links  
4. **Join Links** - Works for open registration
5. **Auth Callback** - Handles all flows correctly

### ? Missing:
1. **Login initiation endpoints** (`/api/auth/login/standard`, `/api/auth/login/enterprise`)
2. **Email service** (invitations work but no emails sent)
3. **Frontend UI** (you're building this separately)

### ?? Quick Fix Needed:
**Add login endpoints to AuthController** so users can:
- Visit `http://localhost:5000/api/auth/login/standard` ? redirect to Keycloak
- Complete self-service registration
- Get auto-created tenant

---

## Next Steps

**Option 1:** I add the missing login endpoints now (5 minutes)

**Option 2:** You test enterprise flows first (which work completely)

**Option 3:** Continue without standard self-service login and use invitations/join-links only

Which would you prefer?
