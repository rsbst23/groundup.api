# User Flows Guide

Complete guide explaining how user creation, login, and tenant assignment flows work.

---

## ?? **Table of Contents**

1. [Flow Overview](#flow-overview)
2. [New User Registration](#new-user-registration)
3. [Existing User Login](#existing-user-login)
4. [Invitation Flow](#invitation-flow)
5. [New Organization Flow](#new-organization-flow)
6. [Multi-Tenant Users](#multi-tenant-users)
7. [Technical Details](#technical-details)

---

## ?? **Flow Overview**

### **Three Main Flows**

```
1. NEW USER REGISTRATION
   User ? Keycloak signup ? Database sync ? Tenant assignment

2. EXISTING USER LOGIN  
   User ? Keycloak login ? Check tenants ? Navigate appropriately

3. INVITATION FLOW
   Email link ? Keycloak auth ? Auto-accept invitation ? Assign to tenant
```

### **Key Principles**

- ? **Keycloak is source of truth** for authentication
- ? **API syncs users** from Keycloak to local database
- ? **React controls navigation** based on API response
- ? **Tenants determine access** to application features

---

## ?? **New User Registration**

### **Registration Options**

Users can register in three ways:
1. **Email/Password** - Traditional registration form
2. **Social Auth** - Google, Facebook, Microsoft, etc.
3. **Invitation Link** - Pre-authorized access (see Invitation Flow section)

---

### **Flow A: Traditional Email/Password Registration**

```
1. User clicks "Sign Up" in React
   ?
2. React redirects to Keycloak with state parameter
   URL: http://localhost:8080/realms/groundup/...
        ?redirect_uri=http://localhost:5173/auth/callback
        &state=eyJmbG93IjoibmV3X29yZyJ9  (base64: {"flow":"new_org"})
   ?
3. Keycloak shows registration form
   User fills: Email, First Name, Last Name, Password
   ?
4. User clicks "Register"
   ?
5. Keycloak creates user account
   ?
6. Keycloak redirects to React:
   http://localhost:5173/auth/callback?code=ABC123&state=...
   ?
7. React AuthCallback page extracts code
   ?
8. React calls API:
   GET /api/auth/callback?code=ABC123&state=...
   ?
9. API processes:
   a. Exchanges code for tokens with Keycloak
   b. Extracts user ID from JWT token
   c. Gets user details from Keycloak
   d. Syncs user to local database (Users table)
   e. Decodes state parameter ? "new_org" flow
   f. Creates new tenant: "{FirstName}'s Organization"
   g. Assigns user to tenant as admin (UserTenants table)
   h. Generates tenant-scoped JWT token
   i. Sets auth cookie
   j. Returns JSON response
   ?
10. API returns:
    {
      "success": true,
      "flow": "new_org",
      "token": "eyJhbGc...",
      "tenantId": 1,
      "tenantName": "John's Organization",
      "isNewOrganization": true
    }
    ?
11. React navigates to:
    /onboarding?new=true
    ?
12. User sees onboarding wizard
```

---

### **Flow B: Social Authentication Registration (Google)**

```
1. User clicks "Sign Up" in React
   ?
2. React redirects to Keycloak with state parameter
   URL: http://localhost:8080/realms/groundup/...
        ?redirect_uri=http://localhost:5173/auth/callback
        &state=eyJmbG93IjoibmV3X29yZyJ9
   ?
3. Keycloak login page appears with social login options:
   
   ???????????????????????????????
   ?  Sign in to GroundUp        ?
   ?                             ?
   ?  [Sign in with Google]  ??  User clicks this
   ?  [Sign in with Facebook]    ?
   ?  [Sign in with Microsoft]   ?
   ?                             ?
   ?  ?????????? OR ??????????   ?
   ?                             ?
   ?  Username: [___________]    ?
   ?  Password: [___________]    ?
   ?  [Login] [Register]         ?
   ???????????????????????????????
   ?
4. User clicks "Sign in with Google"
   ?
5. Keycloak redirects to Google OAuth:
   https://accounts.google.com/o/oauth2/v2/auth
   ?client_id=keycloak-google-client-id
   &redirect_uri=http://localhost:8080/realms/groundup/broker/google/endpoint
   &response_type=code
   &scope=openid email profile
   ?
6. Google login page appears:
   
   ???????????????????????????????
   ?  Sign in with Google        ?
   ?                             ?
   ?  Email: [john@gmail.com]    ?
   ?  [Next]                     ?
   ?                             ?
   ?  OR                         ?
   ?                             ?
   ?  [Choose another account]   ?
   ?  [Create account]           ?
   ???????????????????????????????
   ?
7. User enters Google credentials and authorizes
   ?
8. Google redirects back to Keycloak:
   http://localhost:8080/realms/groundup/broker/google/endpoint
   ?code=GOOGLE_AUTH_CODE&state=...
   ?
9. Keycloak exchanges Google code for user info
   ?
10. Keycloak receives from Google:
    {
      "email": "john@gmail.com",
      "given_name": "John",
      "family_name": "Doe",
      "picture": "https://lh3.googleusercontent.com/...",
      "email_verified": true,
      "sub": "google-user-id-123456"
    }
    ?
11. Keycloak checks if user exists:
    
    IF user exists (same email):
      ? Link Google account to existing Keycloak user
      ? Continue to login flow
    
    IF user is NEW:
      ? Create new Keycloak user
      ? Username: "john@gmail.com"
      ? Email: "john@gmail.com" (verified automatically!)
      ? First Name: "John"
      ? Last Name: "Doe"
      ? Password: NOT SET (not needed for social auth)
      ? Linked identity: Google (sub: "google-user-id-123456")
    ?
12. Keycloak redirects to React:
    http://localhost:5173/auth/callback?code=ABC123&state=...
    ?
13. React calls API:
    GET /api/auth/callback?code=ABC123&state=...
    ?
14. API processes (same as traditional flow):
    a. Exchanges code for tokens
    b. Extracts user ID from JWT (Keycloak's user ID, not Google's)
    c. Gets user details from Keycloak:
       {
         "id": "keycloak-uuid-abc-123",
         "username": "john@gmail.com",
         "email": "john@gmail.com",
         "firstName": "John",
         "lastName": "Doe",
         "emailVerified": true,  ? Already verified by Google!
         "federatedIdentities": [
           {
             "identityProvider": "google",
             "userId": "google-user-id-123456",
             "userName": "john@gmail.com"
           }
         ]
       }
    d. Syncs user to local database
    e. Decodes state ? "new_org" flow
    f. Creates new tenant: "John's Organization"
    g. Assigns user as admin
    h. Generates tenant-scoped token
    i. Returns JSON
    ?
15. React navigates to /onboarding?new=true
```

**Key Differences with Social Auth:**
- ? **No password required** - User never sets a password
- ? **Email auto-verified** - Google/Facebook already verified it
- ? **Faster registration** - Just click and authorize
- ? **Profile data pre-filled** - Name comes from social provider
- ? **Linked identity stored** - Can login with Google anytime

---

### **Flow C: Social Authentication Registration (Facebook)**

```
1. User clicks "Sign Up" in React
   ?
2. Keycloak login page with social options
   ?
3. User clicks "Sign in with Facebook"
   ?
4. Keycloak redirects to Facebook OAuth:
   https://www.facebook.com/v12.0/dialog/oauth
   ?client_id=keycloak-facebook-app-id
   &redirect_uri=http://localhost:8080/realms/groundup/broker/facebook/endpoint
   &response_type=code
   &scope=email public_profile
   ?
5. Facebook authorization page:
   
   ???????????????????????????????????????
   ?  Log in to continue to GroundUp     ?
   ?                                     ?
   ?  [Profile Photo]                    ?
   ?  John Doe                           ?
   ?  john.doe@facebook.com              ?
   ?                                     ?
   ?  GroundUp will receive:             ?
   ?  ? Your public profile              ?
   ?  ? Your email address               ?
   ?                                     ?
   ?  [Continue as John]                 ?
   ?  [Not You?]                         ?
   ???????????????????????????????????????
   ?
6. User clicks "Continue as John"
   ?
7. Facebook redirects to Keycloak:
   http://localhost:8080/realms/groundup/broker/facebook/endpoint
   ?code=FACEBOOK_AUTH_CODE
   ?
8. Keycloak exchanges Facebook code for user info
   ?
9. Keycloak receives from Facebook:
   {
     "id": "facebook-user-id-789",
     "email": "john.doe@facebook.com",
     "name": "John Doe",
     "first_name": "John",
     "last_name": "Doe",
     "picture": {
       "data": {
         "url": "https://platform-lookaside.fbsbx.com/..."
       }
     }
   }
   ?
10. Keycloak creates/links user (same as Google flow)
    ?
11. Rest of flow identical to Google (steps 12-15)
```

---

### **Flow D: Social Auth - Existing User Linking**

**Scenario:** User registered with email/password, now tries to login with Google using same email.

```
1. User previously registered:
   Email: john@gmail.com
   Method: Email/Password
   ?
2. User clicks "Sign in with Google"
   ?
3. Google authentication succeeds
   ?
4. Keycloak receives:
   Email: john@gmail.com (matches existing user!)
   ?
5. Keycloak behavior (configurable):
   
   Option A: AUTO-LINK (Recommended)
   ? Link Google to existing Keycloak account
   ? User can now login with either:
      • Email/Password
      • Google
   
   Option B: PROMPT USER
   ? "An account with email john@gmail.com already exists"
   ? "Would you like to link your Google account?"
   ? [Yes] [No]
   
   Option C: ERROR (Strict)
   ? "Email already in use"
   ? "Please login with your password"
   ?
6. IF linked successfully:
   User record now shows:
   {
     "federatedIdentities": [
       {
         "identityProvider": "google",
         "userId": "google-user-id-123"
       }
     ]
   }
   ?
7. Continue with normal login flow
```

---

### **Social Auth Configuration in Keycloak**

**To enable Google:**

1. **Keycloak Admin** ? **Identity Providers** ? **Google**
2. Configure:
   - **Client ID:** From Google Cloud Console
   - **Client Secret:** From Google Cloud Console
   - **Default scopes:** `openid email profile`
3. **Advanced Settings:**
   - **First Login Flow:** `first broker login`
   - **Sync mode:** `Import` or `Force`
   - **Trust Email:** ? ON (auto-verify emails from Google)

**To enable Facebook:**

1. **Keycloak Admin** ? **Identity Providers** ? **Facebook**
2. Configure:
   - **App ID:** From Facebook Developers
   - **App Secret:** From Facebook Developers
   - **Default scopes:** `email public_profile`
3. **Advanced Settings:** (same as Google)

---

### **Database State After Social Registration**

**Users table:**
```sql
INSERT INTO Users (Id, Username, Email, FirstName, LastName, IsActive, EmailVerified)
VALUES (
  'keycloak-user-id-guid',
  'john@gmail.com',
  'john@gmail.com',
  'John',
  'Doe',
  1,
  1  -- Auto-verified from Google/Facebook
);
```

**Note:** No password hash in local DB - authentication handled by social provider + Keycloak.

**Keycloak stores the federated identity link:**
```json
{
  "userId": "keycloak-user-id-guid",
  "federatedIdentities": [
    {
      "identityProvider": "google",
      "userId": "google-user-id-123456",
      "userName": "john@gmail.com"
    }
  ]
}
```

---

### **Social Auth User Experience**

**First Registration:**
```
1. Click "Sign Up"
2. Click "Sign in with Google"
3. Google authorization (1 click)
4. Redirect to app
5. Onboarding page
   
Total clicks: 3
Time: ~10 seconds
```

**Subsequent Logins:**
```
1. Click "Login"
2. Click "Sign in with Google"
3. Redirect to app (if already logged into Google)
4. Dashboard

Total clicks: 2
Time: ~5 seconds
```

**vs Traditional:**
```
Registration:
1. Click "Sign Up"
2. Fill email, password, name, etc.
3. Submit form
4. Verify email (check inbox, click link)
5. Redirect to app

Login:
1. Click "Login"
2. Enter email and password
3. Submit
4. Dashboard

Much slower and more friction!
```

---

### **Troubleshooting Social Auth**

**Issue: "Redirect URI mismatch" from Google/Facebook**

**Solution:**
- Check redirect URIs in Google Cloud Console / Facebook App Settings
- Must match: `http://localhost:8080/realms/groundup/broker/google/endpoint`
- For Facebook: `http://localhost:8080/realms/groundup/broker/facebook/endpoint`

**Issue: "Invalid client ID"**

**Solution:**
- Verify Client ID/App ID in Keycloak matches Google/Facebook
- Check Client Secret is correct
- Ensure social app is not in "Development mode" (for production)

**Issue: "Email not received from provider"**

**Solution:**
- Check scopes include `email`
- Some providers require explicit permission request
- User may have denied email permission

**Issue: "User can't login after social registration"**

**Possible Causes:**
- User tries to login with password (they don't have one!)
- User uses different social account with different email
- Identity link broken

**Solution:**
- Direct users to use same social provider they registered with
- OR allow them to set a password via "Forgot Password" flow

---

## ?? **Existing User Login**

### **Login Options**

Existing users can login in multiple ways:
1. **Email/Password** - Traditional credentials
2. **Social Auth** - If they registered or linked a social account
3. **Invitation Link** - Direct access via invitation (auto-login if registered)

---

### **Flow A: Traditional Email/Password Login**

```
1. User clicks "Login" in React
   ?
2. React redirects to Keycloak (no special state)
   URL: http://localhost:8080/realms/groundup/...
        ?redirect_uri=http://localhost:5173/auth/callback
   ?
3. Keycloak shows login form
   ?
4. User enters email and password
   ?
5. Keycloak validates credentials
   ?
6. Keycloak redirects to React:
   http://localhost:5173/auth/callback?code=ABC123
   ?
7. React calls API:
   GET /api/auth/callback?code=ABC123
   ?
8. API processes:
   a. Exchanges code for tokens
   b. Gets user details from Keycloak
   c. Syncs/updates user in database
   d. Checks how many tenants user has:
      
      IF user has 0 tenants:
        - Check for pending invitations
        - If has invitations ? return "hasPendingInvitations: true"
        - If no invitations ? return "no access" message
      
      IF user has 1 tenant:
        - Auto-select that tenant
        - Generate tenant-scoped token
        - Return success with tenant info
      
      IF user has multiple tenants:
        - Return "requiresTenantSelection: true"
        - Include list of available tenants
   ?
9. React navigates based on response:
   
   0 tenants + has invitations ? /pending-invitations
   0 tenants + no invitations ? /no-access
   1 tenant ? /dashboard
   Multiple tenants ? /select-tenant
```

---

### **Flow B: Social Auth Login (Google/Facebook)**

**Scenario:** User previously registered with Google, now logging in again.

```
1. User clicks "Login" in React
   ?
2. React redirects to Keycloak
   ?
3. Keycloak login page shows:
   
   ???????????????????????????????
   ?  Sign in to GroundUp        ?
   ?                             ?
   ?  [Sign in with Google]      ?  ? User's registered method
   ?  [Sign in with Facebook]    ?
   ?                             ?
   ?  ?????????? OR ??????????   ?
   ?                             ?
   ?  Email:    [___________]    ?
   ?  Password: [___________]    ?
   ?  [Login] [Register]         ?
   ???????????????????????????????
   ?
4. User clicks "Sign in with Google"
   ?
5. IF user already logged into Google in browser:
   ? Google auto-authorizes (no login screen!)
   ? Instant redirect back to Keycloak
   ?
   ELSE user not logged into Google:
   ? Google login screen appears
   ? User enters Google credentials
   ? Google redirects back to Keycloak
   ?
6. Keycloak receives Google user info
   ?
7. Keycloak matches to existing user (by linked identity)
   ?
8. Keycloak redirects to React:
   http://localhost:5173/auth/callback?code=ABC123
   ?
9. React calls API (same as traditional login)
   ?
10. API processes:
    - Exchanges code for tokens
    - Gets user from Keycloak (with federated identity info)
    - Updates user in database
    - Checks tenants
    - Returns appropriate response
    ?
11. React navigates (same logic as traditional login)
```

**Speed Comparison:**

**Social Auth Login (if already logged into Google):**
```
1. Click "Login"
2. Click "Sign in with Google"
3. Instant redirect (no credentials needed!)
4. Dashboard appears

Time: ~3 seconds
```

**Traditional Login:**
```
1. Click "Login"
2. Type email
3. Type password
4. Click submit
5. Dashboard appears

Time: ~10-15 seconds
```

---

### **Flow C: Mixed Auth Methods**

**Scenario:** User has both email/password AND social auth linked.

```
User record in Keycloak:
{
  "username": "john@gmail.com",
  "email": "john@gmail.com",
  "credentials": [
    {
      "type": "password",
      "hashedSaltedValue": "..."  ? Has password
    }
  ],
  "federatedIdentities": [
    {
      "identityProvider": "google",  ? Also has Google
      "userId": "google-123"
    }
  ]
}

User can login with EITHER:
1. Email + Password
2. Google button

Both methods authenticate to the same Keycloak user!
```

**Login page shows:**
```
???????????????????????????????
?  Sign in to GroundUp        ?
?                             ?
?  [Sign in with Google] ?    ?  ? Linked
?  [Sign in with Facebook]    ?  ? Not linked
?                             ?
?  ?????????? OR ??????????   ?
?                             ?
?  Email:    [john@gmail.com] ?
?  Password: [************]   ?  ? Also works!
?  [Login]                    ?
???????????????????????????????
```

---

### **Response Examples**

**User with 1 tenant (auto-selected):**
```json
{
  "success": true,
  "flow": "default",
  "token": "eyJhbGc...",
  "tenantId": 1,
  "tenantName": "Acme Corp",
  "requiresTenantSelection": false
}
```

**User with multiple tenants:**
```json
{
  "success": true,
  "flow": "default",
  "requiresTenantSelection": true,
  "availableTenants": [
    {
      "tenantId": 1,
      "tenantName": "Acme Corp",
      "isAdmin": true
    },
    {
      "tenantId": 2,
      "tenantName": "Beta LLC",
      "isAdmin": false
    }
  ]
}
```

**User with pending invitations:**
```json
{
  "success": true,
  "flow": "default",
  "hasPendingInvitations": true,
  "pendingInvitationsCount": 2,
  "requiresTenantSelection": false
}

```

## ?? **Invitation Flow**

### **Overview**

Users can be invited to join existing tenants via email. When they click the invitation link, they can either:
1. **Register with email/password** - Traditional registration
2. **Register with social auth** - Google, Facebook, etc.
3. **Login** (if already registered)

The invitation is automatically accepted after authentication.

---

### **Flow Diagram**

```
1. Tenant admin creates invitation via API:
   POST /api/invitations
   {
     "email": "newuser@example.com",
     "isAdmin": false
   }
   ?
2. API creates invitation in database:
   - Generates unique invitation token (UUID)
   - Sets expiration (7 days)
   - Stores in TenantInvitations table
   ?
3. Email sent to user with invitation link:
   http://localhost:8080/realms/groundup/...
   ?redirect_uri=http://localhost:5173/auth/callback
   &state=eyJmbG93IjoiaW52aXRhdGlvbiIsImludml0YXRpb25Ub2tlbiI6ImFiYzEyMyJ9
   (state = base64: {"flow":"invitation","invitationToken":"abc123..."})
   ?
4. User clicks link ? Keycloak
   ?
5. Keycloak shows login/registration page:
   
   ???????????????????????????????????????
   ?  You've been invited to join        ?
   ?  Acme Corp on GroundUp              ?
   ?                                     ?
   ?  [Sign in with Google]              ?  ? Can use social auth!
   ?  [Sign in with Facebook]            ?
   ?                                     ?
   ?  ?????????? OR ??????????           ?
   ?                                     ?
   ?  New user? Register below:          ?
   ?  Email:      [newuser@example.com]  ?  ? Pre-filled!
   ?  First Name: [___________________]  ?
   ?  Last Name:  [___________________]  ?
   ?  Password:   [___________________]  ?
   ?  [Register]                         ?
   ?                                     ?
   ?  Already have account? [Login]      ?
   ???????????????????????????????????????
   ?
6. User chooses authentication method:
   
   OPTION A: Register with Email/Password
   ? User fills form
   ? Keycloak creates account
   ? Continue to step 7
   
   OPTION B: Register with Google/Facebook
   ? User clicks social button
   ? Redirects to Google/Facebook
   ? User authorizes
   ? Keycloak creates account with social link
   ? Continue to step 7
   
   OPTION C: Login (existing user)
   ? User enters credentials or uses social auth
   ? Keycloak authenticates
   ? Continue to step 7
   ?
7. Keycloak redirects to React:
   http://localhost:5173/auth/callback?code=ABC123&state=...
   ?
8. React calls API with code and state
   ?
9. API processes:
   a. Exchanges code for tokens
   b. Gets user details from Keycloak (including social identity if used)
   c. Syncs user to database
   d. Decodes state ? "invitation" flow
   e. Extracts invitation token from state
   f. Validates invitation:
      - Not expired
      - Email matches (or any email for flexible invitations)
      - Not already accepted
   g. Accepts invitation (marks as accepted in DB)
   h. Assigns user to tenant (creates UserTenant record)
   i. Generates tenant-scoped token
   j. Returns success
   ?
10. API returns:
    {
      "success": true,
      "flow": "invitation",
      "token": "eyJhbGc...",
      "tenantId": 1,
      "tenantName": "Acme Corp",
      "message": "Welcome to Acme Corp!"
    }
    ?
11. React navigates to:
    /dashboard?from=invitation
```

---

### **Invitation with Social Auth - Detailed Flow**

**User clicks invitation link and chooses Google:**

```
1. Click invitation link
   ?
2. Keycloak login page (with state parameter preserved)
   ?
3. User clicks "Sign in with Google"
   ?
4. Google authorization page:
   
   ???????????????????????????????????????
   ?  GroundUp wants to access           ?
   ?  your Google Account                ?
   ?                                     ?
   ?  This will allow GroundUp to:       ?
   ?  ? View your email address          ?
   ?  ? View your basic profile info     ?
   ?                                     ?
   ?  Email: john.doe@gmail.com          ?
   ?                                     ?
   ?  [Cancel] [Allow]                   ?
   ???????????????????????????????????????
   ?
5. User clicks "Allow"
   ?
6. Google redirects to Keycloak:
   http://localhost:8080/realms/groundup/broker/google/endpoint
   ?code=GOOGLE_CODE&state=BROKER_STATE
   ?
7. Keycloak exchanges code for Google user info
   ?
8. Keycloak receives:
   {
     "email": "john.doe@gmail.com",
     "given_name": "John",
     "family_name": "Doe",
     "email_verified": true
   }
   ?
9. Keycloak creates new user (if doesn't exist):
   - Username: john.doe@gmail.com
   - Email: john.doe@gmail.com (verified)
   - First Name: John
   - Last Name: Doe
   - Linked Identity: Google
   ?
10. Keycloak redirects to React WITH ORIGINAL STATE:
    http://localhost:5173/auth/callback
    ?code=ABC123
    &state=eyJmbG93IjoiaW52aXRhdGlvbiIsInRva2VuIjoiLi4uIn0=
    
    ?? CRITICAL: State parameter preserved through entire flow!
    ?
11. React calls API with invitation state
    ?
12. API auto-accepts invitation
    ?
13. User added to tenant
    ?
14. Dashboard with new tenant access!
```

**Key Point:** The invitation `state` parameter flows through:
```
React ? Keycloak ? Google ? Keycloak ? React ? API
```

Keycloak preserves the state throughout the entire social auth flow!

---

### **Email Pre-filling**

**If invitation email matches social auth email:**
```
Invitation sent to: john@gmail.com
User signs in with Google: john@gmail.com

? Emails match ? Invitation auto-accepted
```

**If emails don't match:**
```
Invitation sent to: john@company.com
User signs in with Google: john.doe@gmail.com

?? Emails don't match ? Depends on configuration:

Option A: Reject
  ? "Invitation was sent to john@company.com"
  ? "Please register with that email"

Option B: Allow (flexible invitations)
  ? Accept invitation regardless
  ? Useful for "join our team" links

Option C: Admin approval required
  ? Invitation marked as "pending verification"
  ? Admin gets notification
```

---

### **Database State After Invitation Acceptance**

**User registered with Google:**
```sql
-- Users table
INSERT INTO Users (Id, Username, Email, FirstName, LastName, EmailVerified, IsActive)
VALUES (
  'keycloak-uuid-abc',
  'john.doe@gmail.com',
  'john.doe@gmail.com',
  'John',
  'Doe',
  1,  -- Auto-verified by Google
  1
);
```

**Keycloak stores:**
```json
{
  "id": "keycloak-uuid-abc",
  "username": "john.doe@gmail.com",
  "email": "john.doe@gmail.com",
  "emailVerified": true,
  "federatedIdentities": [
    {
      "identityProvider": "google",
      "userId": "google-user-id-123",
      "userName": "john.doe@gmail.com"
    }
  ]
}
```

**TenantInvitations table (updated):**
```sql
UPDATE TenantInvitations
SET 
  IsAccepted = 1,
  AcceptedAt = GETUTCDATE(),
  AcceptedByUserId = 'keycloak-uuid-abc'
WHERE InvitationToken = 'abc123...';
```


## ?? **Flow Comparison**

| Feature | Registration | Login | Invitation | Social Auth |
|---------|--------------|-------|-----------|-------------|
| **Keycloak action** | Register | Login | Register/Login | OAuth redirect |
| **User creation** | Yes | No (existing) | Yes (if new) | Yes (if new) |
| **Password required** | Yes | Yes | Yes | No |
| **Email verification** | Manual | N/A | Manual | Auto (from provider) |
| **Tenant creation** | Auto (new org) | No | No | Auto (new org) or from invitation |
| **Tenant assignment** | Auto (as admin) | Existing | From invitation | Auto or from invitation |
| **State param** | `{flow: "new_org"}` | None | `{flow: "invitation", token: "..."}` | Preserved from initial request |
| **End result** | User + New Tenant | User + Existing Tenant | User + Invited Tenant | User + Tenant (varies) |
| **Speed** | ~30 seconds | ~10 seconds | ~20 seconds | ~5 seconds |
| **User friction** | High (form filling) | Medium (credentials) | Medium | Low (1-2 clicks) |

---

## ?? **Authentication Method Comparison**

### **Email/Password vs Social Auth**

| Aspect | Email/Password | Social Auth (Google/Facebook) |
|--------|----------------|------------------------------|
| **Registration time** | 30-60 seconds | 5-10 seconds |
| **User data entry** | Manual (email, name, password) | Auto-filled from provider |
| **Email verification** | Required (manual) | Automatic |
| **Password management** | User must create & remember | No password needed |
| **Security** | Depends on user's password | Delegated to Google/Facebook |
| **MFA** | Optional | Often enabled on provider |
| **Login speed** | 10-15 seconds | 3-5 seconds (if already logged in) |
| **Account recovery** | "Forgot password" flow | No recovery needed |
| **Privacy** | All data stays with you | Provider knows user logged in |
| **Setup complexity** | None | OAuth app configuration |

### **When Users Prefer Each Method**

**Users prefer Email/Password when:**
- ?? Privacy-conscious (don't want to share with Google/Facebook)
- ?? Corporate users (company email policy)
- ?? In regions where social providers aren't popular
- ?? Don't have/use social media accounts
- ?? Want to manage passwords themselves

**Users prefer Social Auth when:**
- ? Want fastest registration/login
- ?? Already logged into Google/Facebook
- ?? Don't want to remember another password
- ? Trust the social provider's security
- ?? Mobile users (easier on mobile)
- ?? Using multiple apps (prefer single sign-on)

### **Recommended Approach**

**Offer both options:**
```
???????????????????????????????????
?  Sign up for GroundUp           ?
?                                 ?
?  [Sign up with Google]          ?  ? Primary (most users choose this)
?  [Sign up with Facebook]        ?
?  [Sign up with Microsoft]       ?
?                                 ?
?  ?????????? OR ??????????       ?
?                                 ?
?  Email:    [________________]   ?
?  Password: [________________]   ?  ? Alternative for privacy-conscious
?  [Create Account]               ?
?                                 ?
?  Already have account? [Login]  ?
???????????????????????????????????
```

**Statistics show:**
- ~70% of users choose social auth when available
- ~30% prefer email/password
- Offering both maximizes conversion

---

## ?? **Security Considerations**

### **Social Auth Security**

**Advantages:**
- ? No password storage in your system
- ? MFA often enabled on social accounts
- ? Security updates handled by provider
- ? Professional security teams (Google, Facebook)
- ? Email verification automatic

**Considerations:**
- ?? Dependency on third-party service
- ?? Provider outages affect your login
- ?? Limited control over auth flow
- ?? User data shared with provider

### **Email/Password Security**

**Advantages:**
- ? Full control over auth flow
- ? No dependency on third parties
- ? Can enforce custom password policies
- ? Complete user data privacy

**Considerations:**
- ?? Must implement password security properly
- ?? Must handle password resets
- ?? Must implement MFA yourself
- ?? Users may choose weak passwords

### **Best Practice: Hybrid Approach**

```
? Support both email/password AND social auth
? Allow linking multiple auth methods
? Require MFA for admin users (regardless of method)
? Use Keycloak's built-in security features
? Monitor for suspicious login patterns
```

---

## ?? **User Journey Metrics**

### **Time to First Use**

**New User - Social Auth:**
```
1. Click "Sign Up"               (0s)
2. Click "Sign in with Google"   (2s)
3. Google authorization          (3s)
4. Redirect to app              (5s)
5. Onboarding                   (10s)
??????????????????????????????????
Total: ~10 seconds to start using app
```

**New User - Email/Password:**
```
1. Click "Sign Up"               (0s)
2. Fill email                    (5s)
3. Fill name                     (5s)
4. Create password              (10s)
5. Submit form                  (12s)
6. Check email                  (30s)
7. Click verification link      (32s)
8. Redirect to app             (34s)
9. Onboarding                  (44s)
??????????????????????????????????
Total: ~44 seconds to start using app
```

**340% faster with social auth!**

### **Return User - Login Speed**

**Social Auth (already logged into Google):**
```
1. Click "Login"                 (0s)
2. Click "Sign in with Google"   (1s)
3. Instant redirect             (2s)
4. Dashboard                    (3s)
??????????????????????????????????
Total: ~3 seconds
```

**Email/Password:**
```
1. Click "Login"                 (0s)
2. Type email                    (5s)
3. Type password                (10s)
4. Click submit                 (12s)
5. Dashboard                    (14s)
??????????????????????????????????
Total: ~14 seconds
```

**366% faster for returning users!**

---

## ? **Summary**

### **Key Flows**

1. **New User ? New Organization**
   - Email/Password OR Social Auth
   - Auto-create tenant
   - User is admin

2. **Existing User ? Existing Tenant**
   - Email/Password OR Social Auth (if linked)
   - Check tenant count
   - Navigate appropriately

3. **Invited User ? Existing Tenant**
   - Email/Password OR Social Auth
   - Auto-accept invitation
   - Join existing tenant

4. **Social Auth Benefits**
   - 340% faster registration
   - 366% faster login
   - Auto-verified emails
   - Better conversion rates
   - No password management

### **Decision Points**

- ? **0 tenants?** Check for invitations or show "no access"
- ? **1 tenant?** Auto-select and navigate to dashboard
- ? **Multiple tenants?** Show selection page
- ? **State param?** Determines special flows (invitation, new org)
- ? **Social auth?** Faster, auto-verified, no password
- ? **Email/password?** More private, full control

---

**Updated:** 2025-01-21  
**Status:** ? Complete User Flows Documentation with Social Auth  
**Architecture:** Keycloak (with social providers) ? API ? Database ? React
