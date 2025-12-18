# Login vs Registration Flows

## The Problem
Users shouldn't have to see a login page when they click "Sign Up". That's confusing UX!

## The Solution
We now have **two separate entry points**:

### 1. Login Flow (Existing Users)
```
User clicks "Login" ? /api/auth/login ? Keycloak Login Page
```

### 2. Registration Flow (New Users)
```
User clicks "Sign Up" ? /api/auth/register ? Keycloak Registration Page
```
OR
```
User clicks "Sign Up" ? /api/auth/login?action=register ? Keycloak Registration Page
```

---

## API Endpoints

### Standard User Login
```http
GET /api/auth/login
```
**Behavior**: Shows Keycloak **login page**
**Use Case**: User already has account, wants to log in

### Standard User Registration
```http
GET /api/auth/register
```
OR
```http
GET /api/auth/login?action=register
```
**Behavior**: Shows Keycloak **registration page** directly
**Use Case**: New user wants to sign up

### Enterprise User Login
```http
GET /api/auth/login?domain=acme.yourapp.com
```
**Behavior**: Shows Keycloak login page for enterprise realm

### Enterprise User Registration
```http
GET /api/auth/register?domain=acme.yourapp.com
```
OR
```http
GET /api/auth/login?domain=acme.yourapp.com&action=register
```
**Behavior**: Shows Keycloak registration page for enterprise realm

---

## Frontend Implementation

### React Example

```typescript
// Button handlers
function handleLogin() {
  const currentDomain = window.location.host;
  
  // Check if enterprise tenant
  if (currentDomain !== 'yourapp.com' && currentDomain !== 'localhost:5173') {
    // Enterprise login
    window.location.href = `/api/auth/login?domain=${currentDomain}`;
  } else {
    // Standard login
    window.location.href = '/api/auth/login';
  }
}

function handleSignUp() {
  const currentDomain = window.location.host;
  
  // Check if enterprise tenant
  if (currentDomain !== 'yourapp.com' && currentDomain !== 'localhost:5173') {
    // Enterprise registration
    window.location.href = `/api/auth/register?domain=${currentDomain}`;
  } else {
    // Standard registration
    window.location.href = '/api/auth/register';
  }
}
```

### Simple Buttons
```tsx
<button onClick={handleLogin}>
  Login
</button>

<button onClick={handleSignUp}>
  Sign Up
</button>
```

---

## How It Works (Keycloak)

The key is the **`kc_action=REGISTER`** parameter:

### Login URL (default)
```
http://localhost:8080/realms/groundup/protocol/openid-connect/auth
  ?client_id=groundup-api
  &redirect_uri=...
  &response_type=code
  &scope=openid email profile
  &state=...
```
**Result**: Shows login page with "Register" link at bottom

### Registration URL (with kc_action)
```
http://localhost:8080/realms/groundup/protocol/openid-connect/auth
  ?client_id=groundup-api
  &redirect_uri=...
  &response_type=code
  &scope=openid email profile
  &state=...
  &kc_action=REGISTER    <-- This is the key!
```
**Result**: Shows registration page directly

---

## Testing

### Test Standard Registration (Direct to Sign Up)
```bash
# Browser
http://localhost:5123/api/auth/register

# Curl (see redirect URL)
curl -i http://localhost:5123/api/auth/register
```

**Expected**: Redirects to Keycloak **registration page** (not login page)

### Test Standard Login
```bash
# Browser
http://localhost:5123/api/auth/login

# Curl
curl -i http://localhost:5123/api/auth/login
```

**Expected**: Redirects to Keycloak **login page**

### Test Enterprise Registration
```bash
# First create test tenant
INSERT INTO Tenant (Name, RealmName, CustomDomain, TenantType, IsActive, CreatedAt)
VALUES ('Test Corp', 'tenant_test_xyz', 'test.yourapp.com', 1, 1, NOW());

# Then test
curl -i "http://localhost:5123/api/auth/register?domain=test.yourapp.com"
```

**Expected**: Redirects to enterprise realm registration page

---

## User Journey Comparison

### Old Way (Confusing)
```
1. User clicks "Sign Up"
2. Sees LOGIN page ??
3. Has to find tiny "Register" link
4. Clicks it
5. NOW sees registration form
```

### New Way (Clear)
```
1. User clicks "Sign Up"
2. Sees REGISTRATION page immediately ?
3. Fills out form
4. Done!
```

---

## Parameters Summary

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `domain` | string (optional) | Custom domain for enterprise tenants | `acme.yourapp.com` |
| `action` | string (optional) | `login` (default) or `register`/`signup` | `register` |
| `returnUrl` | string (optional) | Future use for post-auth redirect | `/dashboard` |

---

## Backwards Compatibility

? **Old URLs still work**:
- `/api/auth/login` ? login page (unchanged)
- `/api/auth/login/standard` ? redirects to `/api/auth/login`
- `/api/auth/login/enterprise?realm=xyz` ? redirects to `/api/auth/login?domain=xyz`

? **New URLs available**:
- `/api/auth/register` ? registration page
- `/api/auth/login?action=register` ? registration page

---

## Configuration Required

### Keycloak Realm Settings

For the registration flow to work, the realm must have **registration enabled**:

1. Keycloak Admin Console
2. Select realm (e.g., `groundup`)
3. **Realm Settings** ? **Login** tab
4. Enable: **User registration**
5. Save

Without this, the registration page will show an error.

---

## Common Issues

### Error: "Registration is not enabled"
**Cause**: Realm has `registrationAllowed = false`
**Fix**: Enable user registration in Keycloak realm settings

### Registration page shows but then errors
**Cause**: Email verification might be required but SMTP not configured
**Fix**: Either:
- Configure SMTP (see docs/AWS-SES-QUICK-START.md)
- OR disable email verification for testing

### Both login and register go to same page
**Cause**: `kc_action` parameter not being added
**Fix**: Check API logs, ensure `action=register` parameter is received

---

## Summary

**For Login (existing users)**:
```
/api/auth/login
```

**For Registration (new users)**:
```
/api/auth/register
```

**Both support enterprise domains**:
```
/api/auth/login?domain=acme.yourapp.com
/api/auth/register?domain=acme.yourapp.com
```

Much better UX! ??
