# Complete Tenant Flows Design Document

**Purpose:** Comprehensive design document covering both Standard and Enterprise tenant creation and user management flows for detailed discussion and refinement.

**Status:** ?? Ready for Deep Discussion

---

## ?? **Executive Summary**

This document outlines the complete user authentication and tenant management flows for a multi-tenant SaaS application using Keycloak for authentication. The system supports two distinct tenant types:

1. **Standard Tenants** - Free tier, shared Keycloak realm ("groundup")
2. **Enterprise Tenants** - Paid tier, dedicated Keycloak realm per tenant

### **Core Challenge: Cross-Realm User Identity**

Users can exist in multiple Keycloak realms (e.g., a consultant accessing their own company + multiple client enterprises). Our database must track these multiple identities while maintaining a single logical user record.

---

## ?? **Current System Architecture**

### **Database Schema (Current)**

```sql
-- Users: Core user records
Users:
  Id: UNIQUEIDENTIFIER (PRIMARY KEY)
  Email: NVARCHAR(255)
  Username: NVARCHAR(255)
  FirstName: NVARCHAR(255)
  LastName: NVARCHAR(255)
  IsActive: BIT
  CreatedAt: DATETIME2
  LastLoginAt: DATETIME2

-- Tenants: Organizations/companies
Tenants:
  Id: INT IDENTITY (PRIMARY KEY)
  Name: NVARCHAR(255)
  Description: NVARCHAR(1000)
  TenantType: NVARCHAR(50)  -- 'standard' or 'enterprise'
  RealmUrl: NVARCHAR(255)  -- NULL for standard, required for enterprise
  ParentTenantId: INT (nullable)
  IsActive: BIT
  CreatedAt: DATETIME2
  
  -- Computed property: KeycloakRealm
  --   Standard: "groundup" (shared)
  --   Enterprise: Name.ToLowerInvariant() (e.g., "acme")

-- UserTenants: Junction table (many-to-many)
UserTenants:
  Id: INT IDENTITY (PRIMARY KEY)
  UserId: UNIQUEIDENTIFIER (FOREIGN KEY ? Users.Id)
  TenantId: INT (FOREIGN KEY ? Tenants.Id)
  IsAdmin: BIT  -- ? Per-tenant admin flag
  JoinedAt: DATETIME2

-- TenantInvitations: Pending user invitations
TenantInvitations:
  Id: INT IDENTITY (PRIMARY KEY)
  Email: NVARCHAR(255)
  TenantId: INT (FOREIGN KEY ? Tenants.Id)
  InvitationToken: NVARCHAR(100) (UNIQUE)
  IsAdmin: BIT
  ExpiresAt: DATETIME2
  IsAccepted: BIT
  AcceptedAt: DATETIME2 (nullable)
  AcceptedByUserId: UNIQUEIDENTIFIER (nullable)
  CreatedByUserId: UNIQUEIDENTIFIER
  CreatedAt: DATETIME2
```

### **Current Problem: Users.Id = Keycloak User ID**

**Issue:** `Users.Id` currently stores the Keycloak user ID from ONE realm only. This breaks cross-realm scenarios.

**Example of the problem:**
```
Scenario: john@consultant.com exists in 3 realms

Current (BROKEN):
Users:
  Id: "kc-groundup-123"  ? Only stores ONE Keycloak ID
  Email: "john@consultant.com"

When john@consultant.com registers in "acme" realm:
- Keycloak creates: "kc-acme-456" (different ID)
- Database lookup by email finds existing user
- But Users.Id still = "kc-groundup-123"
- System can't track the "kc-acme-456" identity
- ? Broken cross-realm support
```

---

## ?? **Proposed Solution: UserKeycloakIdentities Table**

### **New Schema**

```sql
-- Users: Global user records (email is the true identifier)
Users:
  Id: UNIQUEIDENTIFIER (PRIMARY KEY)  ? Generate OUR OWN GUID
  Email: NVARCHAR(255) UNIQUE  ? The true global identifier
  Username: NVARCHAR(255)
  FirstName: NVARCHAR(255)
  LastName: NVARCHAR(255)
  IsActive: BIT
  CreatedAt: DATETIME2
  LastLoginAt: DATETIME2

-- UserKeycloakIdentities: Track user identities across realms (NEW)
UserKeycloakIdentities:
  Id: INT IDENTITY (PRIMARY KEY)
  UserId: UNIQUEIDENTIFIER (FOREIGN KEY ? Users.Id)
  RealmName: NVARCHAR(255)  -- "groundup", "acme", "beta", etc.
  KeycloakUserId: NVARCHAR(255)  -- Keycloak's user ID in that realm
  CreatedAt: DATETIME2
  
  UNIQUE INDEX: (RealmName, KeycloakUserId)  -- Prevent duplicates
  INDEX: (UserId)  -- Fast lookups by global user ID
```

### **How It Works**

**Single-realm user (standard tenant):**
```
Users:
  Id: "guid-abc-123"  ? Our generated GUID
  Email: "alice@example.com"

UserKeycloakIdentities:
  UserId: "guid-abc-123"
  RealmName: "groundup"
  KeycloakUserId: "kc-groundup-789"
```

**Multi-realm user (consultant accessing 3 enterprise clients):**
```
Users:
  Id: "guid-xyz-999"  ? Same global user
  Email: "john@consultant.com"

UserKeycloakIdentities:
  [1] UserId: "guid-xyz-999", RealmName: "groundup", KeycloakUserId: "kc-groundup-111"
  [2] UserId: "guid-xyz-999", RealmName: "acme",     KeycloakUserId: "kc-acme-222"
  [3] UserId: "guid-xyz-999", RealmName: "beta",     KeycloakUserId: "kc-beta-333"
  [4] UserId: "guid-xyz-999", RealmName: "gamma",    KeycloakUserId: "kc-gamma-444"

UserTenants:
  UserId: "guid-xyz-999", TenantId: 1 (Own Company),      IsAdmin: true
  UserId: "guid-xyz-999", TenantId: 2 (Acme Corp),        IsAdmin: false
  UserId: "guid-xyz-999", TenantId: 3 (Beta Industries),  IsAdmin: true
  UserId: "guid-xyz-999", TenantId: 4 (Gamma LLC),        IsAdmin: false
```

**Key Benefits:**
- ? One logical user (`Users.Id`) with multiple Keycloak identities
- ? Email is the global identifier for linking accounts
- ? User can access multiple realms with separate Keycloak accounts
- ? Per-tenant admin permissions via `UserTenants.IsAdmin`

---

## ?? **FLOW 1: Standard Tenant (Current, Working)**

### **Scenario: New User Signs Up for Free Plan**

**Tenant Characteristics:**
- TenantType: "standard"
- RealmUrl: NULL
- KeycloakRealm: "groundup" (shared)
- Cost: Free

**User Flow:**

```
STEP 1: User Visits Landing Page
?????????????????????????????????
User: alice@example.com
Action: Clicks "Get Started Free"
Realm: "groundup" (shared realm)


STEP 2: Frontend Redirects to Keycloak
???????????????????????????????????????
Frontend constructs OAuth URL:
http://localhost:8080/realms/groundup/protocol/openid-connect/auth
                          ^^^^^^^^ ? Shared "groundup" realm
  ?client_id=groundup-client
  &redirect_uri=http://localhost:5173/auth/callback
  &response_type=code
  &scope=openid profile email
  &state=eyJmbG93IjoibmV3X29yZyIsInJlYWxtIjoiZ3JvdW5kdXAifQ==
  
State (decoded):
{
  "flow": "new_org",  ? Tells backend to create new tenant
  "realm": "groundup"
}


STEP 3: User Registers in Keycloak
???????????????????????????????????
Keycloak "groundup" realm:
- Shows registration form
- User creates account:
  Email: alice@example.com
  Password: ********
  First Name: Alice
  Last Name: Smith

Keycloak creates user in "groundup" realm:
{
  "id": "kc-groundup-123",  ? Keycloak-generated ID
  "email": "alice@example.com",
  "firstName": "Alice",
  "lastName": "Smith"
}


STEP 4: Keycloak Redirects to App
??????????????????????????????????
http://localhost:5173/auth/callback
  ?code=xyz789...
  &state=eyJmbG93IjoibmV3X29yZyIsInJlYWxtIjoiZ3JvdW5kdXAifQ==


STEP 5: React Calls Backend Callback
?????????????????????????????????????
Frontend calls:
GET /api/auth/callback?code=xyz789&state=eyJ...


STEP 6: Backend Processes Authentication
?????????????????????????????????????????
AuthController.AuthCallback():

a. Parse state ? flow = "new_org", realm = "groundup"

b. Exchange code with Keycloak "groundup" realm:
   POST http://localhost:8080/realms/groundup/protocol/openid-connect/token
   Returns: access_token, refresh_token

c. Extract Keycloak user ID from JWT:
   sub: "kc-groundup-123"
   email: "alice@example.com"

d. Get full user from Keycloak Admin API:
   GET /admin/realms/groundup/users/kc-groundup-123

e. Check if user exists in database BY EMAIL:
   SELECT * FROM Users WHERE Email = 'alice@example.com'
   Result: NOT FOUND

f. Create Users record (PROPOSED NEW APPROACH):
   Users:
     Id: GUID.NewGuid()  ? Generate OUR OWN GUID = "guid-alice-111"
     Email: "alice@example.com"
     Username: "alice@example.com"
     FirstName: "Alice"
     LastName: "Smith"

g. Create UserKeycloakIdentity record (NEW):
   UserKeycloakIdentities:
     UserId: "guid-alice-111"  ? Links to Users.Id
     RealmName: "groundup"
     KeycloakUserId: "kc-groundup-123"  ? Keycloak's ID

h. Handle "new_org" flow ? Create tenant:
   Tenants:
     Id: 1
     Name: "Alice's Organization"
     TenantType: "standard"
     RealmUrl: NULL
     IsActive: true

i. Assign user to tenant as admin:
   UserTenants:
     UserId: "guid-alice-111"
     TenantId: 1
     IsAdmin: true

j. Generate tenant-scoped JWT:
   {
     "sub": "guid-alice-111",  ? Our global user ID
     "email": "alice@example.com",
     "tenant_id": "1",
     "is_admin": "true",
     "realm": "groundup"
   }

k. Set auth cookie and return success


STEP 7: User Lands on Dashboard
????????????????????????????????
Alice is logged in to her standard tenant
Can access app at main domain
```

### **Subsequent Login (Standard Tenant User)**

```
Alice returns later:

1. Frontend redirects to Keycloak "groundup" realm
   (no state parameter needed)

2. Alice logs in with existing credentials

3. Backend callback:
   a. Exchange code ? get access token
   b. Extract Keycloak ID: "kc-groundup-123"
   c. Lookup UserKeycloakIdentity:
      WHERE RealmName = 'groundup' 
      AND KeycloakUserId = 'kc-groundup-123'
      
      Returns: UserId = "guid-alice-111"
   
   d. Get user's tenants:
      SELECT * FROM UserTenants WHERE UserId = 'guid-alice-111'
      
      Returns: TenantId = 1 (Alice's Organization)
   
   e. Generate tenant-scoped token
   f. Alice is logged in
```

---

## ?? **FLOW 2: Enterprise Tenant (Self-Service Signup)**

### **Scenario: Company Signs Up for Enterprise Plan**

**Tenant Characteristics:**
- TenantType: "enterprise"
- RealmUrl: "company.acme.com" (required, unique)
- KeycloakRealm: "acme" (dedicated realm)
- Cost: Paid ($99/mo)

**User Flow:**

```
STEP 1: User Visits Landing Page (Unauthenticated)
???????????????????????????????????????????????????
User: john@acme.com (NO account anywhere yet)
Action: Clicks "Sign Up for Enterprise"


STEP 2: User Fills Enterprise Signup Form
??????????????????????????????????????????
Frontend shows form:
- Company Name: "Acme Corporation"
- Contact Email: john@acme.com
- First Name: John
- Last Name: Doe
- Custom URL: company.acme.com (optional, defaults to acme.myapp.com)
- Payment info (if collecting upfront)

User submits form


STEP 3: Frontend Calls Public API (No Auth Required)
?????????????????????????????????????????????????????
POST /api/tenants/enterprise/signup (AllowAnonymous)
{
  "companyName": "Acme Corporation",
  "contactEmail": "john@acme.com",
  "firstName": "John",
  "lastName": "Doe",
  "customUrl": "company.acme.com"
}


STEP 4: Backend Creates Enterprise Tenant
??????????????????????????????????????????
TenantController.EnterpriseSignup():

a. Validate request:
   - Email format valid
   - Company name not empty
   - Custom URL available (not taken)

b. Generate realm name from company name:
   "Acme Corporation" ? "acme" (lowercase, alphanumeric only)

c. Check if realm name is unique:
   SELECT * FROM Tenants WHERE KeycloakRealm = 'acme'
   
   If exists: Return error "Company name already taken"
   
   Solution for duplicates:
   - Option A: Add suffix (acme-2, acme-3)
   - Option B: Require custom URL to be unique
   - Option C: Generate random realm ID (tenant-{guid})

d. Create Keycloak realm "acme":
   Call: IIdentityProviderAdminService.CreateRealmAsync("acme")
   
   This creates:
   - New Keycloak realm named "acme"
   - Configures default auth providers (Email/Password, Google)
   - Sets basic realm settings
   - Returns success/failure

e. Create Tenant record:
   Tenants:
     Id: 1
     Name: "Acme Corporation"
     TenantType: "enterprise"
     RealmUrl: "company.acme.com"
     KeycloakRealm: "acme"  (computed from Name)
     IsActive: true

f. Create invitation for contact person:
   TenantInvitations:
     Email: "john@acme.com"
     TenantId: 1
     IsAdmin: true
     InvitationToken: "abc123xyz..."
     ExpiresAt: 7 days from now
     IsAccepted: false

g. Send email to john@acme.com:
   Subject: "Welcome to GroundUp - Complete Your Setup"
   
   Body:
   "Thank you for signing up for GroundUp Enterprise!
    
    Your enterprise tenant 'Acme Corporation' has been created.
    
    Click here to complete your registration:
    http://localhost:5173/accept-invitation?token=abc123xyz
    
    This link expires in 7 days."

h. Return success response:
   {
     "success": true,
     "message": "Enterprise tenant created! Check your email.",
     "tenantName": "Acme Corporation",
     "realmUrl": "company.acme.com"
   }


STEP 5: First Admin Receives Invitation Email
??????????????????????????????????????????????
John receives email with invitation link:
http://localhost:5173/accept-invitation?token=abc123xyz


STEP 6: User Clicks Invitation Link
????????????????????????????????????
Frontend loads: /accept-invitation?token=abc123xyz


STEP 7: Frontend Decodes Invitation
????????????????????????????????????
Frontend calls:
GET /api/tenant-invitations/by-token?token=abc123xyz

Backend (TenantInvitationController.GetByToken):
a. Lookup invitation:
   SELECT * FROM TenantInvitations WHERE InvitationToken = 'abc123xyz'

b. Join with Tenants table to get realm info:
   SELECT 
     i.Email,
     i.IsAdmin,
     i.ExpiresAt,
     t.Id AS TenantId,
     t.Name AS TenantName,
     t.TenantType,
     t.RealmUrl,
     t.KeycloakRealm  -- CRITICAL for frontend
   FROM TenantInvitations i
   JOIN Tenants t ON i.TenantId = t.Id
   WHERE i.InvitationToken = 'abc123xyz'

c. Return:
   {
     "email": "john@acme.com",
     "tenantId": 1,
     "tenantName": "Acme Corporation",
     "keycloakRealm": "acme",  ? Frontend needs this
     "realmUrl": "company.acme.com",
     "isAdmin": true,
     "expiresAt": "2025-02-20T00:00:00Z",
     "isExpired": false
   }


STEP 8: Frontend Redirects to Correct Keycloak Realm
?????????????????????????????????????????????????????
Frontend uses invitation data to construct OAuth URL:

http://localhost:8080/realms/acme/protocol/openid-connect/auth
                          ^^^^ ? Enterprise realm (from invitation)
  ?client_id=groundup-client
  &redirect_uri=http://localhost:5173/auth/callback
  &response_type=code
  &scope=openid profile email
  &state=eyJmbG93IjoiaW52aXRhdGlvbiIsInRva2VuIjoiYWJjMTIzIiwicmVhbG0iOiJhY21lIn0=

State (decoded):
{
  "flow": "invitation",
  "token": "abc123xyz",
  "realm": "acme"  ? Backend needs this to exchange token
}


STEP 9: User Registers in "acme" Realm
???????????????????????????????????????
Keycloak "acme" realm displays:
- Registration form (or login if providers configured)
- Acme Corp branding (if configured, or default)
- Only auth providers configured for "acme" realm

John registers (FIRST TIME ANYWHERE):
  Email: john@acme.com
  Password: ********
  First Name: John
  Last Name: Doe

Keycloak creates user in "acme" realm:
{
  "id": "kc-acme-456",  ? Different ID than groundup realm would generate
  "email": "john@acme.com",
  "firstName": "John",
  "lastName": "Doe"
}


STEP 10: Keycloak Redirects to App
???????????????????????????????????
http://localhost:5173/auth/callback
  ?code=xyz789...
  &state=eyJmbG93IjoiaW52aXRhdGlvbiIsInRva2VuIjoiYWJjMTIzIiwicmVhbG0iOiJhY21lIn0=


STEP 11: React Calls Backend Callback
??????????????????????????????????????
GET /api/auth/callback?code=xyz789&state=eyJ...


STEP 12: Backend Processes Enterprise Registration
???????????????????????????????????????????????????
AuthController.AuthCallback():

a. Parse state:
   flow = "invitation"
   token = "abc123xyz"
   realm = "acme"  ? CRITICAL: Use acme realm for token exchange

b. Exchange code with "acme" realm (NOT groundup):
   POST http://localhost:8080/realms/acme/protocol/openid-connect/token
                                   ^^^^ ? Use realm from state
   
   Returns: access_token, refresh_token

c. Extract Keycloak user ID from JWT:
   sub: "kc-acme-456"  ? Keycloak ID in "acme" realm
   email: "john@acme.com"

d. Get full user from Keycloak Admin API (acme realm):
   GET /admin/realms/acme/users/kc-acme-456
                    ^^^^ ? Query acme realm

e. Check if user exists in database BY EMAIL:
   SELECT * FROM Users WHERE Email = 'john@acme.com'
   Result: NOT FOUND (John's first account)

f. Create Users record:
   Users:
     Id: GUID.NewGuid()  ? Generate new GUID = "guid-john-222"
     Email: "john@acme.com"
     Username: "john@acme.com"
     FirstName: "John"
     LastName: "Doe"

g. Create UserKeycloakIdentity record:
   UserKeycloakIdentities:
     UserId: "guid-john-222"
     RealmName: "acme"  ? John only exists in acme realm
     KeycloakUserId: "kc-acme-456"

h. Handle "invitation" flow:
   Call: AcceptInvitationAsync("abc123xyz", "guid-john-222")
   
   This:
   - Updates TenantInvitations.IsAccepted = true
   - Creates UserTenants record:
     UserTenants:
       UserId: "guid-john-222"
       TenantId: 1 (Acme Corporation)
       IsAdmin: true  ? From invitation

i. Generate tenant-scoped JWT:
   {
     "sub": "guid-john-222",
     "email": "john@acme.com",
     "tenant_id": "1",
     "tenant_name": "Acme Corporation",
     "is_admin": "true",
     "realm": "acme"  ? User's current realm
   }

j. Set auth cookie and return success


STEP 13: User Lands on Enterprise Dashboard
????????????????????????????????????????????
John is logged in to Acme Corporation tenant
Can access app at company.acme.com (or main domain)
Has admin privileges


Database State After Enterprise Signup:
????????????????????????????????????????
Tenants:
  Id: 1, Name: "Acme Corporation", TenantType: "enterprise", 
  RealmUrl: "company.acme.com", KeycloakRealm: "acme"

Users:
  Id: "guid-john-222", Email: "john@acme.com"

UserKeycloakIdentities:
  UserId: "guid-john-222", RealmName: "acme", KeycloakUserId: "kc-acme-456"

UserTenants:
  UserId: "guid-john-222", TenantId: 1, IsAdmin: true

TenantInvitations:
  Email: "john@acme.com", TenantId: 1, IsAccepted: true
```

### **Subsequent Enterprise Users**

```
John invites jane@acme.com to Acme Corporation:

STEP 1: John Creates Invitation
????????????????????????????????
POST /api/tenant-invitations
Headers: { "X-Tenant-ID": 1 }
{
  "email": "jane@acme.com",
  "isAdmin": false
}

Backend:
- Creates TenantInvitations record
- Sends email to jane@acme.com


STEP 2: Jane Clicks Invitation Link
????????????????????????????????????
Same flow as John (Steps 5-13):
- Frontend decodes invitation ? realm = "acme"
- Redirects to Keycloak "acme" realm
- Jane registers in "acme" realm
- Gets new Keycloak ID: "kc-acme-789"
- Backend creates:
  Users: Id = "guid-jane-333"
  UserKeycloakIdentities: RealmName = "acme", KeycloakUserId = "kc-acme-789"
  UserTenants: TenantId = 1, IsAdmin = false
```

---

## ?? **FLOW 3: Cross-Realm Scenario (Service Provider)**

### **Scenario: Consultant Accesses Multiple Enterprise Clients**

**Setup:**
- Consultant john@consultant.com has standard tenant in "groundup" realm
- Gets invited to Acme Corp (realm "acme")
- Gets invited to Beta Industries (realm "beta")

**Flow:**

```
INITIAL STATE: John Has Standard Tenant
????????????????????????????????????????
Users:
  Id: "guid-john-999"
  Email: "john@consultant.com"

UserKeycloakIdentities:
  UserId: "guid-john-999", RealmName: "groundup", KeycloakUserId: "kc-gnd-111"

UserTenants:
  UserId: "guid-john-999", TenantId: 1 (Own Company), IsAdmin: true


STEP 1: Acme Corp Invites john@consultant.com
??????????????????????????????????????????????
Acme admin creates invitation:
POST /api/tenant-invitations
{
  "email": "john@consultant.com",
  "isAdmin": false
}

John receives invitation email


STEP 2: John Clicks Acme Invitation
????????????????????????????????????
Frontend decodes invitation ? realm = "acme"
Redirects to Keycloak "acme" realm


STEP 3: John Registers in "acme" Realm
???????????????????????????????????????
John has NEVER been in "acme" realm before
Registers new account:
  Email: john@consultant.com
  Password: ******** (different from groundup realm)

Keycloak creates user in "acme" realm:
{
  "id": "kc-acme-222",  ? NEW Keycloak ID (different from groundup)
  "email": "john@consultant.com"
}


STEP 4: Backend Links Cross-Realm Identity
???????????????????????????????????????????
AuthController.AuthCallback():

a. Exchange code with "acme" realm
b. Extract Keycloak ID: "kc-acme-222"
c. Get email: "john@consultant.com"

d. Check if user exists BY EMAIL:
   SELECT * FROM Users WHERE Email = 'john@consultant.com'
   Result: FOUND (UserId = "guid-john-999")

e. Check if UserKeycloakIdentity exists for this realm:
   SELECT * FROM UserKeycloakIdentities 
   WHERE UserId = 'guid-john-999' 
   AND RealmName = 'acme'
   
   Result: NOT FOUND

f. Create NEW UserKeycloakIdentity:
   UserKeycloakIdentities:
     UserId: "guid-john-999"  ? SAME global user
     RealmName: "acme"
     KeycloakUserId: "kc-acme-222"  ? NEW Keycloak ID

g. Accept invitation:
   UserTenants:
     UserId: "guid-john-999", TenantId: 2 (Acme), IsAdmin: false

h. Generate tenant-scoped token for Acme


DATABASE STATE AFTER ACME INVITATION:
??????????????????????????????????????
Users:
  Id: "guid-john-999", Email: "john@consultant.com"

UserKeycloakIdentities:
  [1] UserId: "guid-john-999", RealmName: "groundup", KeycloakUserId: "kc-gnd-111"
  [2] UserId: "guid-john-999", RealmName: "acme",     KeycloakUserId: "kc-acme-222"

UserTenants:
  [1] UserId: "guid-john-999", TenantId: 1 (Own Company), IsAdmin: true
  [2] UserId: "guid-john-999", TenantId: 2 (Acme Corp),   IsAdmin: false


STEP 5: Beta Industries Invites john@consultant.com
????????????????????????????????????????????????????
Same process:
- Invitation created for Beta (realm "beta")
- John registers in "beta" realm ? "kc-beta-333"
- Backend links via email
- Creates new UserKeycloakIdentity for "beta" realm


FINAL DATABASE STATE:
??????????????????????
Users:
  Id: "guid-john-999", Email: "john@consultant.com"

UserKeycloakIdentities:
  [1] UserId: "guid-john-999", RealmName: "groundup", KeycloakUserId: "kc-gnd-111"
  [2] UserId: "guid-john-999", RealmName: "acme",     KeycloakUserId: "kc-acme-222"
  [3] UserId: "guid-john-999", RealmName: "beta",     KeycloakUserId: "kc-beta-333"

UserTenants:
  [1] UserId: "guid-john-999", TenantId: 1 (Own Company),      IsAdmin: true
  [2] UserId: "guid-john-999", TenantId: 2 (Acme Corp),        IsAdmin: false
  [3] UserId: "guid-john-999", TenantId: 3 (Beta Industries),  IsAdmin: true
```

### **Subsequent Logins for Cross-Realm User**

```
When john@consultant.com wants to access Acme Corp:

1. Frontend needs to know which realm to use
   - Option A: User selects tenant first ? lookup realm
   - Option B: URL-based routing (company.acme.com ? "acme" realm)
   - Option C: Multi-step login (login to groundup, then switch tenant)

2. Frontend redirects to correct Keycloak realm:
   http://localhost:8080/realms/acme/...
   
3. John logs in with Acme credentials

4. Backend callback:
   a. Exchange token with "acme" realm
   b. Get Keycloak ID: "kc-acme-222"
   c. Lookup UserKeycloakIdentity:
      WHERE RealmName = 'acme' AND KeycloakUserId = 'kc-acme-222'
      Returns: UserId = "guid-john-999"
   
   d. Get user's tenants
   e. Generate token for selected tenant
```

---

## ? **Open Design Questions**

### **Question 1: Enterprise Tenant Name Uniqueness**

**Problem:** Two companies both named "Acme Corporation" ? realm "acme"

**Options:**

**A. Add numeric suffix**
```
First: "Acme Corporation" ? realm "acme"
Second: "Acme Corporation" ? realm "acme-2"
Third: "Acme Corporation" ? realm "acme-3"

? Pros: Simple, predictable
? Cons: Ugly URLs (company.acme-2.com)
```

**B. Require unique custom URL**
```
First: "Acme Corporation" ? realmUrl "company.acme.com" ? realm "acme"
Second: "Acme Corporation" ? realmUrl "acme-logistics.com" ? realm "acme-logistics"

? Pros: Clean URLs, forces uniqueness
? Cons: Requires user to own domain
```

**C. Generate random realm IDs**
```
All tenants: realm = "tenant-{guid}"
Display name: "Acme Corporation"
RealmUrl: "company.acme.com" (maps to tenant-{guid})

? Pros: Guaranteed unique
? Cons: Ugly realm names in Keycloak admin
```

**Question: Which approach?**

---

### **Question 2: Email Domain Verification**

**Problem:** User signs up as "Acme Corporation" with john@gmail.com

**Options:**

**A. No verification (MVP)**
```
? Pros: Faster onboarding, supports consultants
? Cons: Could allow squatting ("john@gmail.com" claims "Apple Inc")
```

**B. Domain verification**
```
User provides: admin@acme.com
System sends verification email to: verify@acme.com
Must prove domain ownership before activation

? Pros: Prevents squatting
? Cons: Complex, slows onboarding
```

**Question: Which approach for MVP? Plan for future?**

---

### **Question 3: Cross-Realm Login UX**

**Problem:** john@consultant.com needs to switch between tenants in different realms

**Current Flow:**
```
1. John wants to access Acme Corp
2. Frontend needs to know realm = "acme"
3. How does frontend know?

Option A: User selects tenant first
- Show tenant picker
- Each tenant knows its realm
- Redirect to correct Keycloak realm

Option B: URL-based routing
- company.acme.com ? calls /api/tenants/resolve-realm
- Gets realm = "acme"
- Redirects to "acme" realm

Option C: Hub-and-spoke
- Always login to "groundup" realm first
- Use refresh token exchange to get tokens for other realms
- ? Complex, may not work
```

**Question: How should users navigate between realms?**

---

### **Question 4: Invitation Token in State**

**Current Design:**
```
Invitation link: /accept-invitation?token=abc123

Frontend flow:
1. Decode token ? get realm
2. Redirect to Keycloak with state = {"flow":"invitation","token":"abc123","realm":"acme"}
3. After auth, backend uses token from state to accept invitation
```

**Alternative:**
```
Store invitation token in browser localStorage
After auth callback, frontend calls:
POST /api/tenant-invitations/accept
{ "token": "abc123" }

Backend:
- Uses authenticated user ID from JWT
- Accepts invitation
- Returns success
```

**Question: Which approach is more secure/reliable?**

---

### **Question 5: Standard vs Enterprise Tenant Creation**

**Current:**
- Standard: Auto-created on first login (flow="new_org")
- Enterprise: Created via public signup API, sends invitation

**Alternative:**
- Both: Use public signup API
- Standard: After signup, auto-create user in groundup realm
- Enterprise: After signup, send invitation email

**Question: Should both flows use same signup endpoint with different tenant types?**

---

## ?? **Implementation Changes Needed**

### **1. Database Migration**

```sql
-- Create UserKeycloakIdentities table
CREATE TABLE UserKeycloakIdentities (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  UserId UNIQUEIDENTIFIER NOT NULL,
  RealmName NVARCHAR(255) NOT NULL,
  KeycloakUserId NVARCHAR(255) NOT NULL,
  CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  
  CONSTRAINT FK_UserKeycloakIdentities_Users 
    FOREIGN KEY (UserId) REFERENCES Users(Id),
  
  CONSTRAINT UQ_UserKeycloakIdentities_RealmUser 
    UNIQUE (RealmName, KeycloakUserId)
);

CREATE INDEX IX_UserKeycloakIdentities_UserId 
  ON UserKeycloakIdentities(UserId);

CREATE INDEX IX_Users_Email 
  ON Users(Email);

-- Migrate existing Users data
-- For all existing users, assume they're in "groundup" realm
INSERT INTO UserKeycloakIdentities (UserId, RealmName, KeycloakUserId, CreatedAt)
SELECT Id, 'groundup', CAST(Id AS NVARCHAR(255)), CreatedAt
FROM Users;

-- Note: After migration, Users.Id will no longer equal Keycloak ID
-- New users will get generated GUIDs
```

### **2. Repository Changes**

**IUserRepository - Add method:**
```csharp
Task<ApiResponse<UserDto>> GetByEmailAsync(string email);
```

**IUserKeycloakIdentityRepository - New:**
```csharp
public interface IUserKeycloakIdentityRepository
{
    Task<UserKeycloakIdentity?> GetByRealmAndKeycloakIdAsync(string realm, string keycloakUserId);
    Task<List<UserKeycloakIdentity>> GetByUserIdAsync(Guid userId);
    Task<UserKeycloakIdentity> AddAsync(Guid userId, string realm, string keycloakUserId);
}
```

### **3. Service Changes**

**IIdentityProviderAdminService - Add method:**
```csharp
Task<bool> CreateRealmAsync(string realmName);
```

**Implementation:**
```csharp
public async Task<bool> CreateRealmAsync(string realmName)
{
    // POST /admin/realms
    // Body: { "realm": "acme", "enabled": true }
    // Configure auth providers, realm settings
    // Return success/failure
}
```

### **4. Controller Changes**

**TenantController - Add endpoint:**
```csharp
[HttpPost("enterprise/signup")]
[AllowAnonymous]
public async Task<ActionResult<ApiResponse<EnterpriseSignupResponseDto>>> 
    EnterpriseSignup([FromBody] EnterpriseSignupDto dto)
{
    // 1. Validate
    // 2. Generate realm name
    // 3. Create Keycloak realm
    // 4. Create Tenant record
    // 5. Create invitation
    // 6. Send email
    // 7. Return success
}
```

**TenantInvitationController - Add endpoint:**
```csharp
[HttpGet("by-token")]
[AllowAnonymous]
public async Task<ActionResult<ApiResponse<InvitationDetailsDto>>> 
    GetByToken([FromQuery] string token)
{
    // Lookup invitation
    // Join with Tenants to get realm info
    // Return invitation details including realm
}
```

**AuthController - Update callback:**
```csharp
// In AuthCallback method:
// 1. Parse state to get realm
// 2. Exchange code with CORRECT realm
// 3. Get user from Keycloak (in correct realm)
// 4. Check if Users record exists BY EMAIL
// 5. If not exists: Create Users + UserKeycloakIdentity
// 6. If exists: Check UserKeycloakIdentity for this realm
//    - If not exists: Create new UserKeycloakIdentity
// 7. Continue with flow logic
```

---

## ?? **Summary: What We're Building**

### **Standard Tenant Flow**
1. ? User clicks "Get Started Free"
2. ? Redirects to Keycloak "groundup" realm
3. ? User registers in Keycloak
4. ? Backend creates: Users + UserKeycloakIdentity + Tenant + UserTenants
5. ? User logged in to new standard tenant

### **Enterprise Tenant Flow**
1. ? User fills enterprise signup form (no auth)
2. ? Backend creates: Keycloak realm + Tenant + Invitation
3. ? User receives invitation email
4. ? User clicks link ? redirects to enterprise realm
5. ? User registers in enterprise realm
6. ? Backend creates: Users + UserKeycloakIdentity + UserTenants
7. ? User logged in to enterprise tenant

### **Cross-Realm Flow**
1. ? User exists in realm A
2. ? Gets invited to tenant in realm B
3. ? Registers in realm B (separate Keycloak account)
4. ? Backend links via email ? creates new UserKeycloakIdentity
5. ? User can access both tenants (different realms)

### **Key Tables**
- **Users** - One record per logical user (email is global identifier)
- **UserKeycloakIdentities** - Tracks Keycloak IDs across realms
- **Tenants** - Organizations (standard or enterprise)
- **UserTenants** - User membership in tenants (with IsAdmin flag)
- **TenantInvitations** - Pending user invitations

---

## ?? **Discussion Points for ChatGPT**

1. **Realm name uniqueness** - How to handle duplicate company names?
2. **Email domain verification** - Required for MVP or defer?
3. **Cross-realm login UX** - How do users navigate between tenants in different realms?
4. **Invitation token handling** - State parameter vs localStorage?
5. **Unified signup endpoint** - Should standard and enterprise use same API?
6. **Users.Id migration** - How to migrate existing users to new schema?
7. **Frontend realm resolution** - When/how does frontend determine which realm to use?
8. **Error handling** - What happens if Keycloak realm creation fails?
9. **Billing integration** - When to charge for enterprise signups?
10. **Standard tenant in groundup realm** - Is this assumption still valid?

---

**Document Status:** ?? Ready for Deep Discussion  
**Last Updated:** 2025-01-21  
**Purpose:** Comprehensive design document for ChatGPT discussion to finalize all flows  
**Next Action:** Review and discuss all open questions until we reach consensus
