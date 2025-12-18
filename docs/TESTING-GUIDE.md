# Testing Guide

Complete guide for testing the authentication system.

---

## ?? **Table of Contents**

1. [Prerequisites](#prerequisites)
2. [Manual Testing](#manual-testing)
3. [API Testing](#api-testing)
4. [End-to-End Testing](#end-to-end-testing)
5. [Common Test Scenarios](#common-test-scenarios)

---

## ?? **Prerequisites**

### **Services Running**

```bash
# 1. Start Keycloak
docker-compose -f keycloak-compose.yml up -d

# 2. Start API
cd GroundUp.api
dotnet run

# 3. Start React (in separate terminal)
cd your-react-app
npm run dev
```

### **Verify Services**

| Service | URL | Expected Response |
|---------|-----|-------------------|
| Keycloak | http://localhost:8080 | Keycloak welcome page |
| API | http://localhost:5123/swagger | Swagger UI |
| React | http://localhost:5173 | Your React app |

---

## ?? **Manual Testing**

### **Test 1: New User Registration**

**Steps:**
1. Navigate to React app: `http://localhost:5173`
2. Click "Sign Up" or "Start Free Trial" button
3. Should redirect to Keycloak
4. Click "Register"
5. Fill out form:
   - Email: `test1@example.com`
   - First Name: `Test`
   - Last Name: `User`
   - Password: `Test123!`
6. Click "Register"
7. Should redirect to React `/auth/callback`
8. Should automatically navigate to `/onboarding?new=true`

**Expected Results:**
- ? User created in Keycloak
- ? User synced to database (Users table)
- ? New tenant created (Tenants table)
- ? User assigned to tenant as admin (UserTenants table)
- ? Auth cookie set in browser
- ? Onboarding page displayed

**Verify in Database:**
```sql
-- Check user exists
SELECT * FROM Users WHERE Email = 'test1@example.com';

-- Check tenant created
SELECT * FROM Tenants WHERE Name = 'Test''s Organization';

-- Check user-tenant assignment
SELECT ut.*, t.Name as TenantName
FROM UserTenants ut
JOIN Tenants t ON ut.TenantId = t.Id
JOIN Users u ON ut.UserId = u.Id
WHERE u.Email = 'test1@example.com';
```

---

### **Test 2: Existing User Login**

**Steps:**
1. Navigate to React app
2. Click "Login" button
3. Login with: `test1@example.com` / `Test123!`
4. Should redirect to `/dashboard`

**Expected Results:**
- ? User authenticated
- ? User synced/updated in database
- ? Tenant auto-selected (only 1 tenant)
- ? Auth cookie set
- ? Dashboard displayed

---

### **Test 3: Create and Accept Invitation**

**Part A: Create Invitation**

1. Login as admin user
2. Navigate to Swagger: `http://localhost:5123/swagger`
3. **POST /api/tenants** (create a tenant):
   ```json
   {
     "name": "Test Company",
     "isActive": true
   }
   ```
4. Copy tenant ID from response
5. **POST /api/auth/set-tenant** (set tenant context):
   ```json
   {
     "tenantId": 1
   }
   ```
6. **POST /api/invitations** (create invitation):
   ```json
   {
     "email": "inviteduser@example.com",
     "isAdmin": false
   }
   ```
7. Copy `invitationToken` from response

**Part B: Build Invitation Link**

```typescript
// In browser console or create a test page:
const token = 'YOUR_INVITATION_TOKEN_HERE';
const state = {
  flow: 'invitation',
  invitationToken: token
};
const stateParam = btoa(JSON.stringify(state));

const url = new URL('http://localhost:8080/realms/groundup/protocol/openid-connect/auth');
url.searchParams.set('client_id', 'groundup-api');
url.searchParams.set('redirect_uri', 'http://localhost:5173/auth/callback');
url.searchParams.set('response_type', 'code');
url.searchParams.set('scope', 'openid email profile');
url.searchParams.set('state', stateParam);

console.log(url.toString());
```

**Part C: Accept Invitation**

1. Copy the URL from console
2. Open in new incognito window
3. Click "Register"
4. Fill out form with `inviteduser@example.com`
5. Submit
6. Should redirect to `/dashboard?from=invitation`

**Expected Results:**
- ? User created in Keycloak
- ? User synced to database
- ? Invitation marked as accepted
- ? User assigned to invited tenant
- ? Dashboard displayed

**Verify:**
```sql
-- Check invitation accepted
SELECT * FROM TenantInvitations 
WHERE Email = 'inviteduser@example.com';
-- IsAccepted should be 1

-- Check user assigned
SELECT * FROM UserTenants ut
JOIN Users u ON ut.UserId = u.Id
WHERE u.Email = 'inviteduser@example.com';
```

---

### **Test 4: Multi-Tenant User**

**Setup:**
1. Create second tenant via Swagger
2. Invite existing user to second tenant
3. User accepts invitation

**Test Login:**
1. Logout existing user
2. Login again
3. Should see `/select-tenant` page with list of tenants
4. Click on a tenant
5. Should navigate to `/dashboard`

**Test Switching:**
1. While logged in, click tenant switcher
2. Select different tenant
3. Should refresh with new tenant context

---

## ?? **API Testing**

### **Test Auth Callback Endpoint**

**Note:** You need a real OAuth code from Keycloak.

**Get OAuth Code:**
1. Navigate to:
   ```
   http://localhost:8080/realms/groundup/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http://localhost:5173/auth/callback&response_type=code&scope=openid email profile
   ```
2. Login/Register
3. Copy the `code` parameter from the redirect URL
4. You have ~60 seconds to use it!

**Test with cURL:**
```bash
curl -X GET "http://localhost:5123/api/auth/callback?code=YOUR_CODE_HERE" \
  -H "Accept: application/json" \
  -v
```

**Expected Response:**
```json
{
  "data": {
    "success": true,
    "flow": "default",
    "token": "eyJhbGc...",
    "tenantId": 1,
    "tenantName": "Test Company",
    "requiresTenantSelection": false
  },
  "success": true,
  "statusCode": 200
}
```

---

### **Test Other Endpoints**

**Get User Profile:**
```bash
curl -X GET "http://localhost:5123/api/auth/me" \
  -H "Cookie: AuthToken=YOUR_TOKEN" \
  -H "Accept: application/json"
```

**Set Tenant:**
```bash
curl -X POST "http://localhost:5123/api/auth/set-tenant" \
  -H "Cookie: AuthToken=YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"tenantId": 1}'
```

**Get Invitations:**
```bash
curl -X GET "http://localhost:5123/api/invitations/me" \
  -H "Cookie: AuthToken=YOUR_TOKEN"
```

---

## ?? **End-to-End Testing**

### **Complete Invitation Flow**

```
1. Admin creates tenant
   ?
2. Admin creates invitation
   ?
3. Email sent (manual for now)
   ?
4. User clicks invitation link
   ?
5. User registers at Keycloak
   ?
6. Keycloak redirects to React
   ?
7. React calls API callback
   ?
8. API accepts invitation
   ?
9. User assigned to tenant
   ?
10. User sees dashboard
```

**Verification Points:**
- ? Invitation exists in database
- ? User created in Keycloak
- ? User synced to database
- ? Invitation marked as accepted
- ? UserTenant record created
- ? Auth cookie set
- ? Correct page displayed

---

## ?? **Common Test Scenarios**

### **Scenario 1: User with No Tenants**

**Setup:**
1. Create user in Keycloak manually
2. Don't assign to any tenant

**Test:**
1. User logs in
2. Should see `/no-access` page

**Expected:**
- User authenticated but no tenant access

---

### **Scenario 2: User with Pending Invitations**

**Setup:**
1. Create invitation for email
2. User registers with that email
3. Don't accept invitation yet

**Test:**
1. User logs in
2. Should see `/pending-invitations` page

**Expected:**
- Shows list of pending invitations
- User can accept/decline

---

### **Scenario 3: Expired Invitation**

**Setup:**
1. Create invitation
2. Manually set ExpiresAt to past date in database

**Test:**
1. User tries to use invitation link
2. Should see error message

**Expected:**
- Invitation not accepted
- Error: "Invitation has expired"

---

### **Scenario 4: Already Accepted Invitation**

**Setup:**
1. Create and accept invitation
2. Try to use same link again

**Test:**
1. User clicks invitation link again
2. Should see error or redirect to dashboard

**Expected:**
- Invitation already marked as accepted
- User already has tenant access

---

## ?? **Test Data**

### **Test Users**

| Email | Password | Purpose |
|-------|----------|---------|
| `admin@groundup.local` | `Admin123!` | System admin (Keycloak) |
| `test1@example.com` | `Test123!` | Regular user |
| `test2@example.com` | `Test123!` | Multi-tenant user |
| `inviteduser@example.com` | `Test123!` | Invited user |

### **Test Tenants**

| Name | Description |
|------|-------------|
| `Test's Organization` | Auto-created from registration |
| `Test Company` | Created via API |
| `Acme Corp` | Test tenant 1 |
| `Beta LLC` | Test tenant 2 |

---

## ?? **Debugging Tests**

### **Enable Debug Logging**

**In `appsettings.json`:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "GroundUp.api.Controllers.AuthController": "Debug",
      "GroundUp.infrastructure.services.IdentityProviderService": "Debug",
      "GroundUp.infrastructure.repositories.UserRepository": "Debug"
    }
  }
}
```

### **Check Browser DevTools**

**Network Tab:**
- See API requests/responses
- Check status codes
- View response bodies

**Application Tab:**
- Check cookies (AuthToken)
- View localStorage/sessionStorage

**Console Tab:**
- See JavaScript errors
- Check console.log statements

### **Check Database**

```sql
-- Recent users
SELECT * FROM Users ORDER BY CreatedAt DESC LIMIT 10;

-- Recent tenants
SELECT * FROM Tenants ORDER BY CreatedAt DESC LIMIT 10;

-- User-tenant assignments
SELECT u.Email, t.Name, ut.IsAdmin
FROM UserTenants ut
JOIN Users u ON ut.UserId = u.Id
JOIN Tenants t ON ut.TenantId = t.Id
ORDER BY ut.CreatedAt DESC
LIMIT 10;

-- Recent invitations
SELECT * FROM TenantInvitations 
ORDER BY CreatedAt DESC 
LIMIT 10;
```

---

## ? **Testing Checklist**

- [ ] New user registration works
- [ ] User synced to database
- [ ] New tenant created
- [ ] User assigned as admin
- [ ] Existing user login works
- [ ] Single tenant auto-selected
- [ ] Multiple tenants show selection
- [ ] Invitation created successfully
- [ ] Invitation link works
- [ ] Invitation auto-accepted
- [ ] User assigned to invited tenant
- [ ] Expired invitation rejected
- [ ] Already accepted invitation handled
- [ ] No tenants shows no-access page
- [ ] Pending invitations page works
- [ ] Tenant switching works
- [ ] Auth cookie set correctly
- [ ] All API endpoints return JSON
- [ ] All flows use StatusCode()

---

**Updated:** 2025-01-21  
**Status:** ? Complete Testing Guide  
**Coverage:** Manual, API, and E2E testing
