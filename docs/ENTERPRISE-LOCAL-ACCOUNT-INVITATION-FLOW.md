# Enterprise Tenant: Local Account Invitation Flow

This document explains the complete flow for inviting additional users with local Keycloak accounts to an enterprise tenant, from initial tenant signup through the invited user's first login.

## Table of Contents
1. [Phase 1: Enterprise Tenant Signup (First Admin)](#phase-1-enterprise-tenant-signup-first-admin)
2. [Phase 2: First Admin Creates Invitation](#phase-2-first-admin-creates-invitation)
3. [Phase 3: Invited User Receives Link](#phase-3-invited-user-receives-link)
4. [Phase 4: Invited User Registration/Login](#phase-4-invited-user-registrationlogin)
5. [Phase 5: Auth Callback Processing](#phase-5-auth-callback-processing)
6. [Phase 6: User Access Granted](#phase-6-user-access-granted)

---

## Phase 1: Enterprise Tenant Signup (First Admin)

### Step 1.1: Admin Initiates Signup
The first admin (e.g., company IT admin) initiates enterprise tenant creation.

**Endpoint:** `POST /api/tenants/enterprise/signup`

**Request Body:**
```json
{
  "companyName": "Acme Corporation",
  "contactEmail": "admin@acme.com",
  "contactName": "John Admin",
  "requestedSubdomain": "acme",
  "customDomain": "acme.yourapp.com",
  "plan": "enterprise-trial"
}
```

### Step 1.2: System Creates Keycloak Realm + Tenant

**What happens in the backend:**

1. **Generate unique realm name:**
   ```csharp
   var slug = "acme";
   var shortGuid = Guid.NewGuid().ToString("N").Substring(0, 4);
   var realmName = $"tenant_acme_{shortGuid}"; // e.g., "tenant_acme_a1b2"
   ```

2. **Create dedicated Keycloak realm:**
   ```csharp
   var realmConfig = new CreateRealmDto
   {
       Realm = realmName,
       DisplayName = "Acme Corporation",
       Enabled = true,
       RegistrationAllowed = true,  // ? Enabled for first admin
       RegistrationEmailAsUsername = false,
       LoginWithEmailAllowed = true,
       VerifyEmail = true,
       ResetPasswordAllowed = true
   };
   
   await _identityProviderAdminService.CreateRealmWithClientAsync(
       realmConfig,
       "acme.yourapp.com"  // Custom domain for OAuth redirects
   );
   ```

3. **Create Tenant record:**
   ```csharp
   var tenant = new Tenant
   {
       Name = "Acme Corporation",
       TenantType = TenantType.Enterprise,
       RealmName = "tenant_acme_a1b2",
       CustomDomain = "acme.yourapp.com",
       Plan = "enterprise-trial",
       IsActive = true
   };
   ```

4. **Generate Keycloak registration URL:**
   ```csharp
   var state = new AuthCallbackState
   {
       Flow = "enterprise_first_admin",  // Special flow marker
       Realm = "tenant_acme_a1b2"
   };
   
   var registrationUrl = 
       "https://keycloak.yourapp.com/realms/tenant_acme_a1b2/protocol/openid-connect/registrations" +
       "?client_id=groundup-api" +
       "&redirect_uri=https://api.yourapp.com/api/auth/callback" +
       "&response_type=code" +
       "&scope=openid%20email%20profile" +
       "&state={encoded_state}";
   ```

### Step 1.3: First Admin Registers in Keycloak
Admin is redirected to Keycloak registration page where they create username/password.

### Step 1.4: Auth Callback - First Admin Assignment

After Keycloak registration, user is redirected back to `/api/auth/callback` with authorization code.

**Auth Callback Processing (`HandleEnterpriseFirstAdminFlowAsync`):**

1. **Exchange code for tokens:**
   ```csharp
   var tokenResponse = await _identityProviderService.ExchangeCodeForTokensAsync(
       code, 
       redirectUri, 
       realm: "tenant_acme_a1b2"
   );
   ```

2. **Extract Keycloak user ID (sub claim):**
   ```csharp
   var keycloakUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
   // e.g., "f47ac10b-58cc-4372-a567-0e02b2c3d479"
   ```

3. **Create GroundUp user:**
   ```csharp
   var newUser = new User
   {
       Id = Guid.NewGuid(),  // New GroundUp user ID
       Email = keycloakUser.Email,
       Username = keycloakUser.Username,
       FirstName = keycloakUser.FirstName,
       LastName = keycloakUser.LastName,
       IsActive = true
   };
   ```

4. **Assign user to tenant as admin:**
   ```csharp
   await _userTenantRepository.AssignUserToTenantAsync(
       userId: newUser.Id,
       tenantId: tenant.Id,
       isAdmin: true,
       externalUserId: keycloakUserId  // Store Keycloak sub for future resolution
   );
   ```

   This creates a `UserTenant` record:
   ```csharp
   UserTenant {
       UserId = Guid,                    // GroundUp user ID
       TenantId = 1,                     // Tenant ID
       ExternalUserId = "f47ac10b...",   // Keycloak sub
       IsAdmin = true
   }
   ```

5. **Disable realm registration:**
   ```csharp
   await _identityProviderAdminService.DisableRealmRegistrationAsync(realmName);
   // ? Registration now disabled - future users must be invited
   ```

6. **Return JWT token:**
   ```csharp
   var customToken = await _tokenService.GenerateTokenAsync(
       userId: newUser.Id,
       tenantId: tenant.Id,
       existingClaims: keycloakClaims
   );
   ```

**? Result:** First admin is now authenticated and has admin access to the enterprise tenant.

---

## Phase 2: First Admin Creates Invitation

### Step 2.1: Admin Creates Invitation

The first admin (now logged in) invites another user.

**Endpoint:** `POST /api/invitations`

**Headers:**
```
Authorization: Bearer {admin_jwt_token}
TenantId: 1
```

**Request Body:**
```json
{
  "email": "bob@acme.com",
  "expirationDays": 7,
  "isAdmin": false
}
```

### Step 2.2: System Creates Invitation Record

**Backend Processing (`TenantInvitationRepository.AddAsync`):**

1. **Validate creating user exists:**
   ```csharp
   var creatingUser = await _context.Users.FindAsync(createdByUserId);
   // Ensures admin user is in database
   ```

2. **Get tenant context:**
   ```csharp
   var tenantId = _tenantContext.TenantId;  // From JWT or header
   var tenant = await _context.Tenants.FindAsync(tenantId);
   ```

3. **Create invitation:**
   ```csharp
   var invitation = new TenantInvitation
   {
       ContactEmail = "bob@acme.com",
       TenantId = 1,
       InvitationToken = Guid.NewGuid().ToString("N"),  // 32-char hex
       ExpiresAt = DateTime.UtcNow.AddDays(7),
       Status = InvitationStatus.Pending,
       IsAdmin = false,
       CreatedByUserId = adminUserId,
       CreatedAt = DateTime.UtcNow
   };
   ```

4. **Save to database:**
   ```csharp
   _context.TenantInvitations.Add(invitation);
   await _context.SaveChangesAsync();
   ```

### Step 2.3: System Generates Invitation URL

**The frontend (or email service) constructs invitation URL:**
```
https://acme.yourapp.com/invite/{invitation.InvitationToken}
```

Example:
```
https://acme.yourapp.com/invite/a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6
```

### Step 2.4: Admin Sends Invitation

**Frontend sends email to invited user** with the invitation link.

> **Note:** Email sending is typically handled by frontend or a separate email service. The backend only provides the invitation token and URL structure.

---

## Phase 3: Invited User Receives Link

### Step 3.1: User Clicks Invitation Link

Bob receives email with link:
```
https://acme.yourapp.com/invite/a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6
```

### Step 3.2: Frontend Validates Invitation

**Frontend calls:** `GET /api/invitations/token/{token}`

**Response:**
```json
{
  "data": {
    "id": 5,
    "contactEmail": "bob@acme.com",
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "realmName": "tenant_acme_a1b2",
    "status": "Pending",
    "expiresAt": "2024-01-15T00:00:00Z",
    "isAdmin": false
  },
  "success": true
}
```

### Step 3.3: Frontend Requests Auth URL

**Frontend calls:** `GET /api/invitations/invite/{invitationToken}`

This endpoint validates the invitation and returns Keycloak auth URL.

**Backend Processing (`InvitationController.InviteRedirect`):**

1. **Validate invitation:**
   ```csharp
   var invitation = await _invitationRepo.GetByTokenAsync(invitationToken);
   
   // Check status and expiration
   if (invitation.Status != "Pending" || invitation.ExpiresAt < DateTime.UtcNow)
   {
       return BadRequest("Invitation is no longer valid");
   }
   ```

2. **Build OAuth state:**
   ```csharp
   var state = new AuthCallbackState
   {
       Flow = "invitation",
       InvitationToken = invitationToken,
       Realm = "tenant_acme_a1b2"  // From invitation
   };
   
   var stateEncoded = Convert.ToBase64String(
       Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state))
   );
   ```

3. **Build Keycloak auth URL:**
   ```csharp
   var authUrl = 
       "https://keycloak.yourapp.com/realms/tenant_acme_a1b2/protocol/openid-connect/auth" +
       "?client_id=groundup-api" +
       "&redirect_uri=https://api.yourapp.com/api/auth/callback" +
       "&response_type=code" +
       "&scope=openid%20email%20profile" +
       "&state={encoded_state}";
   ```

**Response:**
```json
{
  "data": {
    "authUrl": "https://keycloak.yourapp.com/realms/tenant_acme_a1b2/protocol/openid-connect/auth?...",
    "action": "invitation"
  },
  "success": true
}
```

---

## Phase 4: Invited User Registration/Login

### Step 4.1: Frontend Redirects to Keycloak

User is redirected to Keycloak login page for realm `tenant_acme_a1b2`.

**What the user sees:**
- **If account exists:** Keycloak login page (username/password)
- **If no account:** User needs to register via admin (registration disabled after first admin)

> **Important:** For enterprise tenants, only the first admin can self-register. Additional users must:
> 1. Be manually created by admin in Keycloak Admin UI, OR
> 2. Use invitation link after being created by admin

### Step 4.2: User Authenticates in Keycloak

**Scenario A: User already has Keycloak account**
- User enters username/password
- Keycloak validates credentials
- Generates authorization code

**Scenario B: User needs account created**
- Admin manually creates user in Keycloak Admin UI
- Sets temporary password
- User receives email with temporary password
- User logs in and sets permanent password

### Step 4.3: Keycloak Redirects to Callback

After successful authentication, Keycloak redirects to:
```
https://api.yourapp.com/api/auth/callback?code={auth_code}&state={encoded_state}
```

---

## Phase 5: Auth Callback Processing

### Step 5.1: Parse State and Route to Invitation Flow

**Auth Callback Handler (`AuthController.AuthCallback`):**

1. **Parse state parameter:**
   ```csharp
   var callbackState = JsonSerializer.Deserialize<AuthCallbackState>(
       Encoding.UTF8.GetString(Convert.FromBase64String(state))
   );
   
   // callbackState.Flow = "invitation"
   // callbackState.InvitationToken = "a1b2c3d4..."
   // callbackState.Realm = "tenant_acme_a1b2"
   ```

2. **Exchange authorization code:**
   ```csharp
   var tokenResponse = await _identityProviderService.ExchangeCodeForTokensAsync(
       code,
       redirectUri,
       realm: "tenant_acme_a1b2"
   );
   ```

3. **Extract Keycloak user ID:**
   ```csharp
   var handler = new JwtSecurityTokenHandler();
   var jwtToken = handler.ReadJwtToken(tokenResponse.AccessToken);
   var keycloakUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
   // e.g., "b92ac10b-78dc-4372-b567-1e03c2d3e489"
   ```

4. **Resolve GroundUp user ID:**
   ```csharp
   var userTenant = await _userTenantRepository.GetByRealmAndExternalUserIdAsync(
       realm: "tenant_acme_a1b2",
       externalUserId: keycloakUserId
   );
   
   Guid? userId = userTenant?.UserId;
   
   // For first-time login: userId will be null
   if (userId == null)
   {
       userId = Guid.NewGuid();  // Create new GroundUp user
   }
   ```

5. **Route to invitation flow:**
   ```csharp
   if (callbackState.Flow == "invitation")
   {
       responseDto = await HandleInvitationFlowAsync(
           userId.Value,
           keycloakUserId,
           realm: "tenant_acme_a1b2",
           invitationToken: callbackState.InvitationToken,
           accessToken: tokenResponse.AccessToken
       );
   }
   ```

### Step 5.2: Process Invitation Flow

**Invitation Flow Handler (`HandleInvitationFlowAsync`):**

1. **Check if GroundUp user exists:**
   ```csharp
   var existingUser = await _dbContext.Users.FindAsync(userId);
   
   if (existingUser == null)
   {
       // First-time user - create GroundUp user record
       var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(
           keycloakUserId,
           realm: "tenant_acme_a1b2"
       );
       
       var newUser = new User
       {
           Id = userId,
           DisplayName = $"{keycloakUser.FirstName} {keycloakUser.LastName}",
           Email = keycloakUser.Email,
           Username = keycloakUser.Username,
           FirstName = keycloakUser.FirstName,
           LastName = keycloakUser.LastName,
           IsActive = true,
           CreatedAt = DateTime.UtcNow
       };
       
       _dbContext.Users.Add(newUser);
       await _dbContext.SaveChangesAsync();
   }
   ```

2. **Accept invitation:**
   ```csharp
   var accepted = await _tenantInvitationRepository.AcceptInvitationAsync(
       invitationToken: "a1b2c3d4...",
       userId: userId,
       externalUserId: keycloakUserId
   );
   ```

   **Inside `AcceptInvitationAsync`:**
   
   a. **Get invitation:**
   ```csharp
   var invitation = await _context.TenantInvitations
       .Include(ti => ti.Tenant)
       .FirstOrDefaultAsync(ti => ti.InvitationToken == token);
   ```
   
   b. **Validate invitation:**
   ```csharp
   if (invitation.Status != InvitationStatus.Pending || invitation.IsExpired)
   {
       return BadRequest("Invalid or expired invitation");
   }
   ```
   
   c. **Verify email matches:**
   ```csharp
   var user = await _context.Users.FindAsync(userId);
   
   if (!user.Email.Equals(invitation.ContactEmail, StringComparison.OrdinalIgnoreCase))
   {
       return BadRequest("Email mismatch");
   }
   ```
   
   d. **Assign user to tenant:**
   ```csharp
   await _userTenantRepo.AssignUserToTenantAsync(
       userId,
       invitation.TenantId,
       isAdmin: invitation.IsAdmin,  // false for Bob
       externalUserId: keycloakUserId
   );
   ```
   
   This creates a `UserTenant` record:
   ```csharp
   UserTenant {
       UserId = Guid (Bob's GroundUp ID),
       TenantId = 1,
       ExternalUserId = "b92ac10b...",  // Bob's Keycloak sub
       IsAdmin = false
   }
   ```
   
   e. **Mark invitation as accepted:**
   ```csharp
   invitation.Status = InvitationStatus.Accepted;
   invitation.AcceptedAt = DateTime.UtcNow;
   invitation.AcceptedByUserId = userId;
   await _context.SaveChangesAsync();
   ```

3. **Get user's tenants:**
   ```csharp
   var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);
   // Should return 1 tenant (Acme Corporation)
   ```

4. **Generate tenant-scoped JWT:**
   ```csharp
   var customToken = await _tokenService.GenerateTokenAsync(
       userId,
       tenantId: userTenants[0].TenantId,
       existingClaims: ExtractClaims(accessToken)
   );
   ```

5. **Set auth cookie:**
   ```csharp
   SetAuthCookie(customToken);
   ```

6. **Return success response:**
   ```csharp
   return new AuthCallbackResponseDto
   {
       Success = true,
       Flow = "invitation",
       Token = customToken,
       TenantId = 1,
       TenantName = "Acme Corporation",
       RequiresTenantSelection = false,
       Message = "Invitation accepted successfully"
   };
   ```

---

## Phase 6: User Access Granted

### Step 6.1: Frontend Receives Success Response

**Response from `/api/auth/callback`:**
```json
{
  "data": {
    "success": true,
    "flow": "invitation",
    "token": "{jwt_token}",
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "requiresTenantSelection": false,
    "message": "Invitation accepted successfully"
  },
  "success": true
}
```

### Step 6.2: Frontend Stores Token & Redirects

1. **Store JWT token** (in cookie or localStorage)
2. **Redirect user** to tenant dashboard/home page

### Step 6.3: User Makes Authenticated Requests

**Example API call:**
```http
GET /api/inventory-items
Authorization: Bearer {jwt_token}
TenantId: 1
```

**JWT Token Contains:**
```json
{
  "ApplicationUserId": "{bob_groundup_id}",
  "TenantId": "1",
  "email": "bob@acme.com",
  "preferred_username": "bob",
  "name": "Bob User",
  "IsAdmin": "false"
}
```

**Backend validates:**
1. Token signature
2. Token expiration
3. User has access to requested tenant
4. User has required permissions for the operation

---

## Key Technical Details

### Identity Resolution

The system uses a two-level identity system:

1. **Keycloak Identity (External):**
   - Managed per-realm
   - User ID = `sub` claim in JWT
   - Example: `"b92ac10b-78dc-4372-b567-1e03c2d3e489"`

2. **GroundUp Identity (Internal):**
   - Global across all tenants
   - User ID = GUID
   - Example: `Guid("12345678-1234-1234-1234-123456789012")`

3. **Mapping (UserTenant.ExternalUserId):**
   ```
   Realm: tenant_acme_a1b2
   + Keycloak Sub: b92ac10b-78dc-4372-b567-1e03c2d3e489
   ? GroundUp User: 12345678-1234-1234-1234-123456789012
   ? Tenant: 1 (Acme Corporation)
   ```

### Database Schema

**After invitation acceptance:**

```sql
-- Users table
INSERT INTO Users (Id, Email, Username, FirstName, LastName, IsActive)
VALUES (
    '12345678-1234-1234-1234-123456789012',
    'bob@acme.com',
    'bob',
    'Bob',
    'User',
    1
);

-- UserTenants table (links user to tenant)
INSERT INTO UserTenants (UserId, TenantId, ExternalUserId, IsAdmin)
VALUES (
    '12345678-1234-1234-1234-123456789012',  -- GroundUp user ID
    1,                                        -- Tenant ID
    'b92ac10b-78dc-4372-b567-1e03c2d3e489',  -- Keycloak sub
    0                                         -- Not admin
);

-- TenantInvitations table
UPDATE TenantInvitations
SET 
    Status = 'Accepted',
    AcceptedAt = '2024-01-10T10:30:00Z',
    AcceptedByUserId = '12345678-1234-1234-1234-123456789012'
WHERE InvitationToken = 'a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6';
```

### Realm Configuration

**Enterprise tenant realm (`tenant_acme_a1b2`):**
- **Registration Allowed:** ? Disabled after first admin
- **Email Verification:** ? Enabled
- **Login with Email:** ? Enabled
- **Reset Password:** ? Enabled
- **OAuth Client:** `groundup-api`
- **Redirect URIs:** `https://acme.yourapp.com/*`, `https://api.yourapp.com/api/auth/callback`

---

## Common Scenarios

### Scenario 1: Existing Keycloak User Accepting Invitation

**User Bob already has Keycloak account in realm `tenant_acme_a1b2`:**

1. Bob clicks invitation link
2. Redirected to Keycloak login
3. Enters username/password
4. Auth callback creates GroundUp user (first time) OR resolves existing user
5. Invitation accepted
6. Bob gets access to tenant

### Scenario 2: New User Needing Account Creation

**User Carol has never logged into `tenant_acme_a1b2`:**

1. Admin creates invitation for carol@acme.com
2. **Admin manually creates Keycloak user** in Admin UI:
   - Username: carol
   - Email: carol@acme.com
   - Temporary password: SetTemp123!
3. Admin sends invitation link to Carol
4. Carol clicks link
5. Carol logs in with temporary password
6. Keycloak forces password reset
7. Auth callback processes invitation
8. Carol gets access to tenant

### Scenario 3: User Invitation Expires

**Invitation expired before acceptance:**

1. User clicks invitation link
2. Frontend calls `/api/invitations/token/{token}`
3. Backend validates:
   ```csharp
   if (invitation.ExpiresAt < DateTime.UtcNow)
   {
       return BadRequest("Invitation has expired");
   }
   ```
4. Frontend shows "Invitation expired" message
5. User must contact admin for new invitation

### Scenario 4: Email Mismatch

**User logs in with wrong account:**

1. Invitation sent to bob@acme.com
2. User accidentally logs in with alice@acme.com account
3. Auth callback accepts invitation
4. `AcceptInvitationAsync` validates email:
   ```csharp
   if (!user.Email.Equals(invitation.ContactEmail, StringComparison.OrdinalIgnoreCase))
   {
       return BadRequest("Email mismatch");
   }
   ```
5. Invitation NOT accepted
6. User sees error message
7. Must log in with correct account

---

## Security Considerations

### 1. Invitation Token Security
- 32-character hexadecimal string (128-bit entropy)
- Single-use (status changed to "Accepted")
- Time-limited (default 7 days)
- Cannot be reused after acceptance

### 2. Email Verification
- Keycloak enforces email verification
- User must verify email before accessing resources
- Prevents unauthorized access

### 3. Realm Isolation
- Each enterprise tenant has dedicated Keycloak realm
- User accounts are realm-specific
- No cross-realm authentication

### 4. Registration Control
- Registration disabled after first admin
- Additional users must be:
  - Manually created by admin in Keycloak
  - Invited via invitation system
- Prevents unauthorized signups

### 5. Token Security
- JWT tokens signed with secret key
- Short expiration (1 hour default)
- HttpOnly cookies prevent XSS
- Secure flag requires HTTPS

---

## Troubleshooting

### Issue: "Invitation not found"
**Cause:** Invalid or expired invitation token  
**Solution:** Admin should create new invitation

### Issue: "Email mismatch"
**Cause:** User logged in with wrong Keycloak account  
**Solution:** User must log out and log in with invited email

### Issue: "User not found in Keycloak"
**Cause:** User hasn't been created in Keycloak yet  
**Solution:** Admin must create Keycloak user manually first

### Issue: "Invitation already accepted"
**Cause:** Token was already used  
**Solution:** User should log in directly (not through invitation link)

### Issue: "Registration disabled"
**Cause:** User trying to self-register after first admin  
**Solution:** Admin must create user account and send invitation

---

## API Reference

### Key Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/tenants/enterprise/signup` | POST | None | Create enterprise tenant |
| `/api/invitations` | POST | Admin | Create invitation |
| `/api/invitations/token/{token}` | GET | None | Get invitation details |
| `/api/invitations/invite/{token}` | GET | None | Get Keycloak auth URL |
| `/api/auth/callback` | GET | None | OAuth callback handler |
| `/api/invitations/accept` | POST | User | Accept invitation (manual) |

### State Object Structure

```typescript
interface AuthCallbackState {
  flow: "invitation" | "enterprise_first_admin" | "default" | "join_link" | "new_org";
  invitationToken?: string;
  realm: string;
  joinToken?: string;
}
```

### User Tenant Record

```csharp
public class UserTenant
{
    public Guid UserId { get; set; }           // GroundUp user ID
    public int TenantId { get; set; }          // Tenant ID
    public string? ExternalUserId { get; set; } // Keycloak sub claim
    public bool IsAdmin { get; set; }          // Admin flag
    
    // Navigation properties
    public User? User { get; set; }
    public Tenant? Tenant { get; set; }
}
```

---

## Summary

The enterprise local account invitation flow involves:

1. ? Enterprise tenant creation with dedicated Keycloak realm
2. ? First admin self-registration (registration then disabled)
3. ? Admin creates invitation for new user
4. ? Admin manually creates Keycloak account for user (via Admin UI)
5. ? User clicks invitation link
6. ? User authenticates in Keycloak (dedicated realm)
7. ? Auth callback processes invitation
8. ? User assigned to tenant with proper role
9. ? User receives JWT token and gains access

**Key Difference from Standard Tenants:**
- Standard tenants: Users share `groundup` realm, can self-register
- Enterprise tenants: Dedicated realm per tenant, registration disabled after first admin, users manually created by admin
