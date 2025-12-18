# Standard Tenant: New User Signup Flow

Complete step-by-step documentation of how a brand new user creates a standard tenant account.

---

## ?? **Scenario**

- **User:** John (doesn't have an account anywhere)
- **URL:** `acme.com` (could be any URL - no tenant exists yet)
- **Plan:** Standard (free tier)
- **Goal:** Create account + get a tenant to use the app

---

## ?? **Complete Flow Diagram**

```
Landing Page ? Keycloak Registration ? Email Verification ? 
API Callback ? Create User in DB ? Create Tenant ? 
Assign User to Tenant ? Generate Token ? Onboarding
```

---

## ?? **Detailed Step-by-Step Flow**

### **Phase 1: Landing Page**

```
1. John visits: http://acme.com
   ?
2. Frontend tries to resolve realm:
   GET /api/tenants/resolve-realm?url=acme.com
   ?
3. API Response:
   {
     "realm": "groundup",      ? Default realm (no tenant found for acme.com)
     "tenantId": null,
     "tenantName": null,
     "isEnterprise": false
   }
   ?
4. Frontend shows marketing/landing page:
   
   ???????????????????????????????????????
   ?  Welcome to GroundUp                ?
   ?  The best inventory management      ?
   ?                                     ?
   ?  [Sign Up - It's Free!]  ? John clicks
   ?  [Login]                            ?
   ???????????????????????????????????????
```

---

### **Phase 2: Keycloak Registration**

```
5. Frontend redirects to Keycloak "groundup" realm:
   
   http://localhost:8080/realms/groundup/protocol/openid-connect/auth
   ?client_id=groundup-app
   &redirect_uri=http://localhost:5173/auth/callback
   &response_type=code
   &scope=openid email profile
   &state=eyJmbG93IjoibmV3X29yZyIsInJlYWxtIjoiZ3JvdW5kdXAifQ==
   
   State decoded: {
     "flow": "new_org",
     "realm": "groundup"
   }
   ?
6. Keycloak login/register page appears:
   
   ???????????????????????????????????????
   ?  Sign in to GroundUp                ?
   ?                                     ?
   ?  [Sign in with Google]              ?
   ?  [Sign in with Facebook]            ?
   ?                                     ?
   ?  ?????????? OR ??????????           ?
   ?                                     ?
   ?  New user? Register below:          ?
   ?  Email:      [___________________]  ?
   ?  First Name: [___________________]  ?
   ?  Last Name:  [___________________]  ?
   ?  Password:   [___________________]  ?
   ?  [Register]                         ?
   ?                                     ?
   ?  Already have account? [Login]      ?
   ???????????????????????????????????????
   ?
7. John fills out the form:
   - Email: john@example.com
   - First Name: John
   - Last Name: Doe
   - Password: SecurePass123!
   ?
8. John clicks [Register]
```

---

### **Phase 3: Keycloak Creates User**

```
9. Keycloak validates the registration:
   - Email unique? ?
   - Password meets requirements? ?
   - Required fields present? ?
   ?
10. Keycloak creates user in "groundup" realm:
    
    Keycloak User Record:
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",  ? Keycloak generates this
      "username": "john@example.com",
      "email": "john@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "emailVerified": false,  ? NOT verified yet!
      "enabled": true,
      "credentials": [
        {
          "type": "password",
          "hashedValue": "..."  ? Hashed password
        }
      ],
      "requiredActions": ["VERIFY_EMAIL"]  ? Forces email verification
    }
    ?
11. Keycloak shows verification screen:
    
    ???????????????????????????????????????
    ?  Verify Your Email Address          ?
    ?                                     ?
    ?  We've sent a verification email to ?
    ?  john@example.com                   ?
    ?                                     ?
    ?  Please check your inbox and click  ?
    ?  the verification link.             ?
    ?                                     ?
    ?  [Resend Email]                     ?
    ???????????????????????????????????????
```

---

### **Phase 4: Email Verification**

```
12. John receives email:
    
    Subject: Verify your email for GroundUp
    
    Hi John,
    
    Please verify your email by clicking the link below:
    
    [Verify Email Address]
    ?
    http://localhost:8080/realms/groundup/login-actions/action-token
    ?key=eyJhbGc...verification-token
    &client_id=groundup-app
    ?
13. John clicks the verification link
    ?
14. Keycloak verifies email:
    - Marks emailVerified = true
    - Removes VERIFY_EMAIL from requiredActions
    ?
15. Keycloak shows success page:
    
    ???????????????????????????????????????
    ?  Email Verified!                    ?
    ?                                     ?
    ?  Your email has been verified.      ?
    ?  You can now access GroundUp.       ?
    ?                                     ?
    ?  [Continue to Application]          ?
    ???????????????????????????????????????
    ?
16. John clicks [Continue to Application]
    ?
17. Keycloak redirects to React app:
    
    http://localhost:5173/auth/callback
    ?code=AUTH_CODE_ABC123
    &state=eyJmbG93IjoibmV3X29yZyIsInJlYWxtIjoiZ3JvdW5kdXAifQ==
```

---

### **Phase 5: API Authentication Callback**

```
18. React receives callback:
    - Code: AUTH_CODE_ABC123
    - State: {"flow":"new_org","realm":"groundup"}
    ?
19. React calls API:
    GET /api/auth/callback
    ?code=AUTH_CODE_ABC123
    &state=eyJmbG93IjoibmV3X29yZyIsInJlYWxtIjoiZ3JvdW5kdXAifQ==
    ?
20. API AuthController processes:
    
    a. Decode state parameter:
       {
         "flow": "new_org",
         "realm": "groundup"
       }
    
    b. Exchange code for tokens with Keycloak "groundup" realm:
       POST http://localhost:8080/realms/groundup/protocol/openid-connect/token
       {
         "grant_type": "authorization_code",
         "code": "AUTH_CODE_ABC123",
         "client_id": "groundup-app",
         "client_secret": "...",
         "redirect_uri": "http://localhost:5173/auth/callback"
       }
    
    c. Keycloak returns tokens:
       {
         "access_token": "eyJhbGc...",
         "refresh_token": "eyJhbGc...",
         "id_token": "eyJhbGc...",
         "token_type": "Bearer",
         "expires_in": 300
       }
    
    d. Extract user ID from JWT token:
       - Decode access_token
       - Extract "sub" claim = "550e8400-e29b-41d4-a716-446655440000"
    
    e. Get user details from Keycloak:
       GET http://localhost:8080/admin/realms/groundup/users/550e8400-e29b-41d4-a716-446655440000
       
       Response:
       {
         "id": "550e8400-e29b-41d4-a716-446655440000",
         "username": "john@example.com",
         "email": "john@example.com",
         "firstName": "John",
         "lastName": "Doe",
         "emailVerified": true,  ? Now verified!
         "enabled": true
       }
```

---

### **Phase 6: Create User in Our Database**

```
21. API checks if user exists in our database:
    
    SELECT * FROM Users WHERE Email = 'john@example.com'
    
    Result: NOT FOUND (new user)
    ?
22. API creates user in our database:
    
    INSERT INTO Users (Id, Username, Email, FirstName, LastName, EmailVerified, IsActive)
    VALUES (
      '550e8400-e29b-41d4-a716-446655440000',  ? Same ID as Keycloak!
      'john@example.com',
      'john@example.com',
      'John',
      'Doe',
      1,  ? Email verified
      1   ? Active
    )
    
    Our Database Now Has:
    Users:
      Id: "550e8400-e29b-41d4-a716-446655440000"
      Email: "john@example.com"
      FirstName: "John"
      LastName: "Doe"
      EmailVerified: true
      IsActive: true
      CreatedAt: 2025-01-21 15:30:00
```

---

### **Phase 7: Create Standard Tenant**

```
23. API sees state.flow = "new_org"
    ?
24. API creates standard tenant:
    
    INSERT INTO Tenants (Name, TenantType, IsActive)
    VALUES (
      'John''s Organization',  ? Auto-generated from first name
      'standard',
      1
    )
    
    Tenant Created:
      Id: 1
      Name: "John's Organization"
      TenantType: "standard"
      RealmUrl: NULL  ? Standard tenants don't have custom URLs
      RealmName: NULL  ? Uses default "groundup" realm
      IsActive: true
      CreatedAt: 2025-01-21 15:30:00
```

---

### **Phase 8: Assign User to Tenant as Admin**

```
25. API assigns user to tenant:
    
    INSERT INTO UserTenants (UserId, TenantId, IsAdmin)
    VALUES (
      '550e8400-e29b-41d4-a716-446655440000',
      1,
      1  ? User is admin of this tenant
    )
    
    UserTenants Record:
      UserId: "550e8400-e29b-41d4-a716-446655440000"
      TenantId: 1
      IsAdmin: true
      CreatedAt: 2025-01-21 15:30:00
```

---

### **Phase 9: Generate Tenant-Scoped Token**

```
26. API generates custom JWT token with tenant claim:
    
    Token Payload:
    {
      "sub": "550e8400-e29b-41d4-a716-446655440000",  ? User ID
      "email": "john@example.com",
      "name": "John Doe",
      "tenant_id": 1,  ? Tenant scope!
      "tenant_name": "John's Organization",
      "is_admin": true,  ? Admin flag for this tenant
      "iat": 1737471000,
      "exp": 1737474600
    }
    ?
27. API sets auth cookie:
    
    Set-Cookie: AuthToken=eyJhbGc...; 
                HttpOnly; 
                Secure; 
                SameSite=Lax; 
                Path=/; 
                Expires=...
```

---

### **Phase 10: Return Success Response**

```
28. API returns JSON:
    
    {
      "success": true,
      "flow": "new_org",
      "token": "eyJhbGc...",
      "tenantId": 1,
      "tenantName": "John's Organization",
      "requiresTenantSelection": false,
      "isNewOrganization": true,
      "message": "Welcome to GroundUp!"
    }
    ?
29. React receives response
    ?
30. React navigates to onboarding:
    
    /onboarding?new=true
    ?
31. John sees onboarding wizard:
    
    ???????????????????????????????????????
    ?  Welcome to GroundUp, John!         ?
    ?                                     ?
    ?  Let's get you started...           ?
    ?                                     ?
    ?  Step 1: Tell us about your org     ?
    ?  [Next]                             ?
    ???????????????????????????????????????
```

---

## ?? **Database State After Completion**

### **Keycloak "groundup" Realm**

```json
User:
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "username": "john@example.com",
  "email": "john@example.com",
  "emailVerified": true,
  "firstName": "John",
  "lastName": "Doe",
  "enabled": true,
  "credentials": [
    {
      "type": "password",
      "hashedValue": "..."
    }
  ]
}
```

### **Our Application Database**

```sql
-- Users table
Users:
  Id: "550e8400-e29b-41d4-a716-446655440000"
  Username: "john@example.com"
  Email: "john@example.com"
  FirstName: "John"
  LastName: "Doe"
  EmailVerified: 1
  IsActive: 1
  CreatedAt: 2025-01-21 15:30:00

-- Tenants table
Tenants:
  Id: 1
  Name: "John's Organization"
  TenantType: "standard"
  RealmUrl: NULL
  RealmName: NULL
  IsActive: 1
  CreatedAt: 2025-01-21 15:30:00

-- UserTenants table
UserTenants:
  UserId: "550e8400-e29b-41d4-a716-446655440000"
  TenantId: 1
  IsAdmin: 1
  CreatedAt: 2025-01-21 15:30:00
```

---

## ?? **Key Principles**

### **1. Single Keycloak Identity**
- User created ONLY in "groundup" realm
- ONE Keycloak user ID: `550e8400-e29b-41d4-a716-446655440000`
- We use the SAME ID in our database
- No user duplication across realms (for standard tenants)

### **2. Standard Tenant Characteristics**
- **No custom URL** (RealmUrl = NULL)
- **No dedicated realm** (RealmName = NULL)
- **Always uses "groundup" realm** for authentication
- **User is automatically admin** (IsAdmin = true)
- **TenantType = "standard"**

### **3. Email Verification Required**
- User MUST verify email before continuing
- Keycloak handles the verification flow
- User receives email, clicks link, then completes registration
- EmailVerified flag set to true after verification

### **4. Tenant Auto-Created**
- Named after user: `"{FirstName}'s Organization"`
- Type: "standard"
- User automatically assigned as admin
- No additional configuration needed

### **5. Token Contains Tenant Scope**
- Our custom JWT has `tenant_id` claim
- All subsequent API calls include tenant context
- User can ONLY access their own tenant's data
- Middleware validates tenant access on every request

---

## ?? **Authentication Flow Summary**

```
User Action ? Keycloak ? Our API ? Database ? Response
     ?           ?          ?          ?          ?
  Sign Up ? Register ? Verify ? Sync ? Create ? Token ? Onboard
```

### **What Keycloak Does:**
- ? User registration
- ? Email verification
- ? Password hashing
- ? OAuth token generation
- ? Session management

### **What Our API Does:**
- ? Token exchange
- ? User sync to local DB
- ? Tenant creation
- ? User-tenant assignment
- ? Custom token generation
- ? Cookie management

### **What Our Database Stores:**
- ? User records (synced from Keycloak)
- ? Tenant records
- ? UserTenant mappings
- ? Application-specific data

---

## ?? **Timeline**

```
Step 1-4:   Landing page                    (0-5 seconds)
Step 5-8:   Registration form              (5-30 seconds)
Step 9-11:  Keycloak creates user          (1-2 seconds)
Step 12-17: Email verification             (varies - user dependent)
Step 18-27: API processing                 (2-3 seconds)
Step 28-31: Navigate to onboarding         (1 second)

Total: ~10-45 seconds (depending on email verification speed)
```

---

## ?? **Security Considerations**

### **Email Verification**
- ? Required before full account access
- ? Prevents spam accounts
- ? Validates email ownership
- ? Keycloak-managed (secure)

### **Password Security**
- ? Password never stored in our database
- ? Keycloak handles hashing (bcrypt)
- ? Password complexity enforced
- ? Secure credential storage

### **Token Security**
- ? HttpOnly cookies (no JavaScript access)
- ? Secure flag (HTTPS only)
- ? SameSite=Lax (CSRF protection)
- ? Short expiration (1 hour)
- ? Tenant-scoped claims

### **Data Isolation**
- ? Tenant ID in every token
- ? Database queries filtered by tenant
- ? User can only access their tenant's data
- ? Middleware enforces tenant context

---

## ?? **User Experience**

### **First-Time User Journey**

```
1. Discover landing page         (Curiosity)
   ?
2. Click "Sign Up"               (Interest)
   ?
3. Fill registration form        (Commitment)
   ?
4. Check email                   (Validation)
   ?
5. Click verification link       (Activation)
   ?
6. See onboarding wizard         (Engagement)
   ?
7. Start using the app           (Value delivery)
```

### **Design Principles**
- ? **Minimal friction** - Few steps to value
- ? **Clear progress** - User knows what's happening
- ? **Automatic setup** - Tenant created automatically
- ? **Admin by default** - User has full control
- ? **Guided onboarding** - Wizard helps setup

---

## ?? **Code References**

### **Key Files Involved**

```
Frontend:
  - Landing page component
  - Auth callback page
  - Onboarding wizard

Backend:
  - AuthController.cs (auth callback endpoint)
  - UserRepository.cs (user sync)
  - TenantRepository.cs (tenant creation)
  - UserTenantRepository.cs (user-tenant assignment)
  - TokenService.cs (token generation)

Keycloak:
  - "groundup" realm configuration
  - Email verification settings
  - OAuth client settings
```

### **API Endpoints Used**

```
GET  /api/tenants/resolve-realm?url={url}
GET  /api/auth/callback?code={code}&state={state}
POST /api/tenants (internal - tenant creation)
POST /api/user-tenants (internal - assignment)
```

---

## ?? **Next Steps After Onboarding**

Once John completes the flow:

1. **Customize organization settings**
   - Update organization name
   - Add logo/branding
   - Configure preferences

2. **Invite team members**
   - Send invitations
   - Assign roles
   - Share access

3. **Start using features**
   - Create inventory items
   - Manage collections
   - Generate reports

4. **Explore advanced features**
   - Configure integrations
   - Set up automation
   - Customize workflows

---

## ? **Success Criteria**

User signup is successful when:

- ? User exists in Keycloak "groundup" realm
- ? Email is verified (emailVerified = true)
- ? User synced to our database
- ? Standard tenant created
- ? User assigned as admin to tenant
- ? Tenant-scoped token generated
- ? Cookie set in browser
- ? User redirected to onboarding

---

## ?? **Alternative Flows**

### **Social Auth Registration**
See: [Social Auth Flow Documentation](#)
- Skip email verification (Google/Facebook already verified)
- Auto-fill user profile data
- Faster registration process

### **Invitation-Based Signup**
See: [Invitation Flow Documentation](#)
- User receives invitation link
- Registers and auto-joins existing tenant
- No new tenant created

---

## ?? **Related Documentation**

- [User Flows Guide](./USER-FLOWS-GUIDE.md)
- [Social Auth Setup](./SOCIAL-AUTH-SETUP.md)
- [Multi-Realm Architecture](./MULTI-REALM-REFACTORING-COMPLETE.md)
- [Authentication Wiki](./Authentication-Wiki.md)
- [Tenant Management](./TENANT-MANAGEMENT-SUMMARY.md)

---

**Document Status:** ? Complete  
**Last Updated:** 2025-01-21  
**Applicable To:** Standard Tenant New User Signup  
**Architecture:** Keycloak (groundup realm) ? API ? Database ? React
