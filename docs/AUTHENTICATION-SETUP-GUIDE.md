# Authentication Setup Guide

Complete guide for setting up Keycloak authentication with your React + .NET API architecture.

---

## ?? **Table of Contents**

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Keycloak Setup](#keycloak-setup)
4. [API Configuration](#api-configuration)
5. [React Configuration](#react-configuration)
6. [Testing](#testing)
7. [Troubleshooting](#troubleshooting)

---

## ?? **Overview**

### **Architecture**

```
???????????????????
?  React (5173)   ?  User Interface
?   Frontend      ?
???????????????????
         ? API Calls (JSON)
         ?
???????????????????
?  .NET API       ?  Business Logic
?    (5123)       ?  Database Access
???????????????????
         ? OAuth/Token Exchange
         ?
???????????????????
?  Keycloak       ?  Authentication
?    (8080)       ?  User Management
???????????????????
```

### **Authentication Flow**

```
1. User clicks "Login" in React
   ?
2. React redirects to Keycloak
   ?
3. User authenticates at Keycloak
   ?
4. Keycloak redirects back to React (/auth/callback)
   ?
5. React extracts OAuth code from URL
   ?
6. React calls API: GET /api/auth/callback?code=...
   ?
7. API exchanges code for tokens with Keycloak
   ?
8. API syncs user to database
   ?
9. API assigns user to tenant(s)
   ?
10. API returns JSON with auth status
    ?
11. React navigates based on response
```

---

## ?? **Prerequisites**

### **Required Software**

- ? Docker Desktop (for Keycloak)
- ? .NET 8 SDK
- ? Node.js 18+ (for React)
- ? MySQL/PostgreSQL (for database)

### **Your URLs**

| Component | URL | Port |
|-----------|-----|------|
| Keycloak | `http://localhost:8080` | 8080 |
| .NET API | `http://localhost:5123` | 5123 |
| React App | `http://localhost:5173` | 5173 |

---

## ?? **Keycloak Setup**

### **Step 1: Start Keycloak**

```bash
# From your project root
docker-compose -f keycloak-compose.yml up -d
```

**Verify Keycloak is running:**
```bash
# Check container
docker ps | grep keycloak

# Access admin console
# Navigate to: http://localhost:8080/admin
# Login: admin / admin
```

### **Step 2: Create Realm**

1. Click dropdown in top-left (says "master")
2. Click **"Create Realm"**
3. **Realm name:** `groundup`
4. Click **"Create"**

### **Step 3: Create Client**

1. Left sidebar ? **Clients**
2. Click **"Create client"**

**General Settings:**
- **Client type:** OpenID Connect
- **Client ID:** `groundup-api`
- Click **"Next"**

**Capability config:**
- **Client authentication:** ? ON
- **Authorization:** ? OFF
- **Standard flow:** ? ON (REQUIRED!)
- **Direct access grants:** ? ON
- **Implicit flow:** ? OFF
- **Service accounts roles:** ? ON
- Click **"Next"**

**Login settings:**
- **Root URL:** `http://localhost:5173`
- **Home URL:** `http://localhost:5173`
- **Valid redirect URIs:** `http://localhost:5173/auth/callback*`
- **Valid post logout redirect URIs:** `http://localhost:5173/*`
- **Web origins:** 
  ```
  http://localhost:5173
  http://localhost:5123
  +
  ```
- Click **"Save"**

### **Step 4: Get Client Secret**

1. Go to **"Credentials"** tab
2. Copy the **Client secret**
3. Save it - you'll need it for `.env` file

### **Step 5: Configure Realm Settings**

**Realm settings ? Login tab:**

| Setting | Value | Why |
|---------|-------|-----|
| User registration | ? ON | Self-service signup |
| Forgot password | ? ON | Password reset |
| Remember me | ? ON | Better UX |
| Email as username | ? ON | Login with email |
| Login with email | ? ON | Email login |
| Verify email | ? OFF | For dev (ON for production) |
| Edit username | ? OFF | Prevent changes |

Click **"Save"**

### **Step 6: Create Roles**

1. Left sidebar ? **Realm roles**
2. Create these roles:

**SYSTEMADMIN:**
- **Role name:** `SYSTEMADMIN`
- **Description:** `Full system access`
- Click **"Save"**

**Admin:**
- **Role name:** `Admin`
- **Description:** `Tenant administrator`
- Click **"Save"**

**Member:**
- **Role name:** `Member`
- **Description:** `Regular tenant member`
- Click **"Save"**

### **Step 7: Create Test Admin User**

1. Left sidebar ? **Users**
2. Click **"Add user"**

**User details:**
- **Email:** `admin@groundup.local`
- **Email verified:** ? ON
- **First name:** `System`
- **Last name:** `Admin`
- **Enabled:** ? ON
- Click **"Create"**

**Set password:**
1. Go to **"Credentials"** tab
2. Click **"Set password"**
3. **Password:** `Admin123!`
4. **Temporary:** ? OFF
5. Click **"Save"** ? Confirm

**Assign role:**
1. Go to **"Role mapping"** tab
2. Click **"Assign role"**
3. Select **SYSTEMADMIN**
4. Click **"Assign"**

### **Step 8: Verify Configuration**

**Test OpenID configuration:**
```
http://localhost:8080/realms/groundup/.well-known/openid-configuration
```
Should return JSON with endpoints.

**Test login page:**
```
http://localhost:8080/realms/groundup/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http://localhost:5173/auth/callback&response_type=code&scope=openid email profile
```
Should show Keycloak login page with "Register" link.

---

## ?? **API Configuration**

### **Step 1: Environment Variables**

Create/update `.env` file in `GroundUp.api/`:

```bash
# Keycloak Configuration
KEYCLOAK_AUTH_SERVER_URL=http://localhost:8080
KEYCLOAK_REALM=groundup
KEYCLOAK_RESOURCE=groundup-api
KEYCLOAK_CLIENT_SECRET=YOUR-CLIENT-SECRET-FROM-KEYCLOAK

# Keycloak Admin (for user management)
KEYCLOAK_ADMIN_CLIENT_ID=admin-cli
KEYCLOAK_ADMIN_CLIENT_SECRET=YOUR-ADMIN-SECRET

# JWT Configuration (for custom tokens)
JWT_SECRET=supersecretkeythatshouldbe32charsmin!
JWT_ISSUER=GroundUp
JWT_AUDIENCE=GroundUpUsers

# Database
ConnectionStrings__DefaultConnection=your-connection-string
```

### **Step 2: Verify API is Configured**

The API is already configured with:
- ? Dual JWT authentication (Keycloak + Custom)
- ? Auth callback endpoint (`GET /api/auth/callback`)
- ? User sync service
- ? Tenant-scoped tokens
- ? Permission system

**No code changes needed!**

### **Step 3: Run the API**

```bash
cd GroundUp.api
dotnet run
```

**Verify API is running:**
```
http://localhost:5123/swagger
```

---

## ?? **React Configuration**

### **Step 1: Create Auth Callback Page**

```typescript
// src/pages/AuthCallback.tsx
import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

export default function AuthCallback() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const handleCallback = async () => {
      const code = searchParams.get('code');
      const state = searchParams.get('state');

      if (!code) {
        setError('No authorization code received');
        setLoading(false);
        return;
      }

      try {
        // Call API auth callback
        const response = await fetch(
          `http://localhost:5123/api/auth/callback?code=${code}&state=${state || ''}`,
          {
            credentials: 'include',
            headers: { 'Accept': 'application/json' }
          }
        );

        const result = await response.json();

        if (!result.success) {
          setError(result.data?.errorMessage || 'Authentication failed');
          setLoading(false);
          return;
        }

        const data = result.data;

        // Handle different flows
        if (data.requiresTenantSelection) {
          navigate('/select-tenant', { state: { tenants: data.availableTenants } });
        } else if (data.hasPendingInvitations) {
          navigate('/pending-invitations');
        } else if (!data.tenantId) {
          navigate('/no-access');
        } else {
          // Store tenant info
          sessionStorage.setItem('tenantId', data.tenantId.toString());
          sessionStorage.setItem('tenantName', data.tenantName || '');

          // Navigate based on flow
          if (data.flow === 'new_org' && data.isNewOrganization) {
            navigate('/onboarding?new=true');
          } else if (data.flow === 'invitation') {
            navigate('/dashboard?from=invitation');
          } else {
            const returnUrl = sessionStorage.getItem('returnUrl') || '/dashboard';
            sessionStorage.removeItem('returnUrl');
            navigate(returnUrl);
          }
        }
      } catch (err) {
        console.error('Auth callback error:', err);
        setError('An unexpected error occurred');
        setLoading(false);
      }
    };

    handleCallback();
  }, [searchParams, navigate]);

  if (loading) {
    return <div>Processing authentication...</div>;
  }

  if (error) {
    return (
      <div>
        <h1>Authentication Error</h1>
        <p>{error}</p>
        <button onClick={() => navigate('/login')}>Try Again</button>
      </div>
    );
  }

  return null;
}
```

### **Step 2: Create Login Button**

```typescript
// src/components/LoginButton.tsx
export default function LoginButton() {
  const handleLogin = () => {
    const keycloakUrl = new URL('http://localhost:8080/realms/groundup/protocol/openid-connect/auth');
    keycloakUrl.searchParams.set('client_id', 'groundup-api');
    keycloakUrl.searchParams.set('redirect_uri', 'http://localhost:5173/auth/callback');
    keycloakUrl.searchParams.set('response_type', 'code');
    keycloakUrl.searchParams.set('scope', 'openid email profile');
    
    // Save current page for return after login
    sessionStorage.setItem('returnUrl', window.location.pathname);
    
    window.location.href = keycloakUrl.toString();
  };
  
  return <button onClick={handleLogin}>Login</button>;
}
```

### **Step 3: Add Routes**

```typescript
// src/App.tsx or routes.tsx
import AuthCallback from './pages/AuthCallback';
import Dashboard from './pages/Dashboard';
import SelectTenant from './pages/SelectTenant';
import PendingInvitations from './pages/PendingInvitations';
import NoAccess from './pages/NoAccess';
import Onboarding from './pages/Onboarding';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/auth/callback" element={<AuthCallback />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/select-tenant" element={<SelectTenant />} />
        <Route path="/pending-invitations" element={<PendingInvitations />} />
        <Route path="/no-access" element={<NoAccess />} />
        <Route path="/onboarding" element={<Onboarding />} />
      </Routes>
    </BrowserRouter>
  );
}
```

---

## ?? **Testing**

### **Test 1: Login Flow**

1. Start Keycloak, API, and React
2. Navigate to React app: `http://localhost:5173`
3. Click "Login" button
4. Should redirect to Keycloak
5. Login with: `admin@groundup.local` / `Admin123!`
6. Should redirect back to React `/auth/callback`
7. Should process and navigate to appropriate page

### **Test 2: Registration Flow**

1. Navigate to Keycloak login page
2. Click "Register"
3. Fill out registration form
4. Submit
5. Should redirect to React callback
6. User should be synced to database

### **Test 3: API Endpoint**

```bash
# Get auth code from browser URL after Keycloak redirect
# Then test API directly:

curl -X GET "http://localhost:5123/api/auth/callback?code=YOUR_CODE_HERE" \
  -H "Accept: application/json"
```

Should return JSON with auth status.

---

## ?? **Troubleshooting**

### **"Redirect URI mismatch"**

**Problem:** Keycloak rejects redirect

**Solution:**
- Check Keycloak client settings
- Valid redirect URIs must include: `http://localhost:5173/auth/callback*`
- The `*` is required!

### **"Invalid client credentials"**

**Problem:** Token exchange fails

**Solution:**
- Check `KEYCLOAK_CLIENT_SECRET` in `.env`
- Verify it matches Keycloak **Credentials** tab
- Client authentication must be ON

### **"User registration not showing"**

**Problem:** No "Register" link on Keycloak login

**Solution:**
- Realm settings ? Login tab
- User registration must be ON

### **CORS errors**

**Problem:** Browser blocks API calls

**Solution:**
- Web origins in Keycloak must include:
  - `http://localhost:5173`
  - `http://localhost:5123`
  - `+`

### **Cookie not set**

**Problem:** Auth token cookie not appearing

**Solution:**
- API calls must use `credentials: 'include'`
- Check browser DevTools ? Application ? Cookies

---

## ? **Configuration Checklist**

### **Keycloak:**
- [ ] Realm `groundup` created
- [ ] Client `groundup-api` configured
- [ ] Client authentication ON
- [ ] Standard flow enabled
- [ ] Redirect URI: `http://localhost:5173/auth/callback*`
- [ ] Web origins configured
- [ ] User registration enabled
- [ ] SYSTEMADMIN role created
- [ ] Test user created

### **API:**
- [ ] `.env` file configured
- [ ] Client secret set
- [ ] API running on port 5123
- [ ] Swagger accessible

### **React:**
- [ ] `/auth/callback` page created
- [ ] Login button implemented
- [ ] Routes configured
- [ ] API calls use `credentials: 'include'`
- [ ] Running on port 5173

---

## ?? **Next Steps**

1. **Test the complete flow** end-to-end
2. **Create additional pages** (dashboard, onboarding, etc.)
3. **Set up email** (optional for dev, required for production)
4. **Configure social login** (Google, Microsoft, etc.)

---

**Updated:** 2025-01-21  
**Status:** ? Complete Setup Guide  
**Architecture:** React (5173) ? API (5123) ? Keycloak (8080)
