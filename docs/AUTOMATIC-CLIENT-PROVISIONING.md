# ?? Automatic Keycloak Client Provisioning

## Overview

When creating an enterprise tenant, the system now **automatically creates and configures** the `groundup-api` OAuth2 client in the new Keycloak realm. This eliminates the need for manual client configuration and ensures consistent setup across all enterprise tenants.

---

## What Changed

### **Before (Manual Configuration Required)**

1. Create enterprise tenant ? Realm created in Keycloak
2. ? Manually go to Keycloak Admin Console
3. ? Manually create `groundup-api` client
4. ? Manually configure redirect URIs
5. ? Manually configure web origins
6. ? Manually set client secret
7. ? Test authentication

**Problem:** Users would get "Invalid redirect_uri" errors if client wasn't configured correctly.

### **After (Fully Automated)**

1. Create enterprise tenant ? Realm **AND** client created automatically ?
2. Test authentication immediately ?

**Result:** Zero manual configuration needed!

---

## Implementation Details

### **1. New Environment Variables**

Added to `.env`:

```sh
# Frontend URLs (SPA)
FRONTEND_URL=http://localhost:5173
FRONTEND_CALLBACK_PATH=/auth/callback

# Backend API URLs
API_URL=http://localhost:5000
API_CALLBACK_PATH=/api/auth/callback

# Production URLs (update these for production deployment)
PRODUCTION_FRONTEND_URL=https://app.yourdomain.com
PRODUCTION_API_URL=https://api.yourdomain.com
```

### **2. New DTO: `CreateClientDto`**

Location: `GroundUp.Core/dtos/RealmDtos.cs`

```csharp
public class CreateClientDto
{
    public string ClientId { get; set; }
    public bool Confidential { get; set; }
    public string? ClientSecret { get; set; }
    public List<string> RedirectUris { get; set; }
    public List<string> WebOrigins { get; set; }
    public bool StandardFlowEnabled { get; set; }
    public bool DirectAccessGrantsEnabled { get; set; }
    public bool ImplicitFlowEnabled { get; set; }
}
```

### **3. New Interface Method**

Location: `GroundUp.Core/interfaces/IIdentityProviderAdminService.cs`

```csharp
Task<ApiResponse<bool>> CreateClientInRealmAsync(string realmName, CreateClientDto dto);
```

### **4. Implementation in `IdentityProviderAdminService`**

Location: `GroundUp.infrastructure/services/IdentityProviderAdminService.cs`

#### **New Methods:**

1. **`CreateClientInRealmAsync`** - Creates OAuth2 client in Keycloak realm
2. **`BuildClientConfiguration`** - Builds client config from environment variables

#### **Updated Method:**

- **`CreateRealmAsync`** - Now calls `CreateClientInRealmAsync` after creating realm

---

## Client Configuration

The automatically created client has these settings:

| Setting | Value | Purpose |
|---------|-------|---------|
| **Client ID** | `groundup-api` | From `KEYCLOAK_RESOURCE` env var |
| **Confidential** | `true` | Requires client secret (secure) |
| **Client Secret** | From env var | From `KEYCLOAK_CLIENT_SECRET` |
| **Standard Flow** | Enabled | Authorization Code Flow (OAuth2) |
| **Implicit Flow** | Disabled | Not recommended for security |
| **Direct Access Grants** | Disabled | Password flow not allowed |
| **PKCE** | `S256` | Enhanced security for auth code flow |

### **Redirect URIs (Automatically Configured)**

Development:
```
http://localhost:5173/auth/callback
http://localhost:5173/*
http://localhost:5000/api/auth/callback
```

Production (if configured):
```
https://app.yourdomain.com/auth/callback
https://app.yourdomain.com/*
https://api.yourdomain.com/api/auth/callback
```

### **Web Origins (CORS)**

Development:
```
http://localhost:5173
http://localhost:5000
```

Production (if configured):
```
https://app.yourdomain.com
https://api.yourdomain.com
```

---

## How It Works

### **Flow Diagram**

```
Enterprise Tenant Creation
    ?
Create Realm in Keycloak
    ?
Build Client Configuration
  ?? Get redirect URIs from env vars
  ?? Get client secret from config
  ?? Build CreateClientDto
    ?
Create Client in New Realm
  ?? POST to /admin/realms/{realm}/clients
  ?? Set redirect URIs
  ?? Set web origins
  ?? Configure OAuth2 flows
    ?
Return Success
```

### **Code Flow**

```csharp
// 1. TenantRepository calls IdentityProviderAdminService
var realmResult = await _identityProviderAdminService.CreateRealmAsync(realmDto);

// 2. CreateRealmAsync creates realm
var response = await _httpClient.PostAsJsonAsync(requestUrl, realmPayload);

// 3. Then automatically creates client
var clientDto = BuildClientConfiguration(); // From env vars
var clientResult = await CreateClientInRealmAsync(dto.Realm, clientDto);

// 4. BuildClientConfiguration reads env vars
var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
var apiUrl = Environment.GetEnvironmentVariable("API_URL");
// ... builds redirect URIs
```

---

## Benefits

### **For Development**

- ? **Zero manual setup** - Works immediately after creating tenant
- ? **Consistent configuration** - All realms configured identically
- ? **Faster testing** - No manual Keycloak Admin Console steps

### **For Production**

- ? **Environment-based config** - Different URLs for dev/staging/prod
- ? **Automated deployment** - No manual post-deployment steps
- ? **Reduced errors** - No risk of misconfigured clients

### **For Security**

- ? **PKCE enabled** - Enhanced security for auth code flow
- ? **No password flow** - Direct access grants disabled
- ? **No implicit flow** - Deprecated flow disabled
- ? **Proper CORS** - Web origins configured correctly

---

## Configuration for Different Environments

### **Local Development**

`.env`:
```sh
FRONTEND_URL=http://localhost:5173
API_URL=http://localhost:5000
```

Result:
- Frontend redirects to `http://localhost:5173/auth/callback`
- Backend redirects to `http://localhost:5000/api/auth/callback`

### **QA/Staging**

`.env`:
```sh
FRONTEND_URL=http://localhost:5173
API_URL=http://localhost:5000
PRODUCTION_FRONTEND_URL=https://qa.yourdomain.com
PRODUCTION_API_URL=https://api-qa.yourdomain.com
```

Result:
- **Both** localhost and QA URLs configured
- Can test locally or on QA server

### **Production**

`.env`:
```sh
FRONTEND_URL=http://localhost:5173
API_URL=http://localhost:5000
PRODUCTION_FRONTEND_URL=https://app.yourdomain.com
PRODUCTION_API_URL=https://api.yourdomain.com
```

Result:
- **Both** localhost and production URLs configured
- Localhost for admin testing, production for users

---

## Testing

### **Verify Automatic Creation**

1. Create an enterprise tenant:
```bash
curl -X POST "http://localhost:5000/api/tenants/enterprise/signup" \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Test Corp",
    "contactEmail": "admin@test.com",
    "contactName": "Test Admin",
    "requestedSubdomain": "test",
    "plan": "enterprise-trial"
  }'
```

2. Check Keycloak Admin Console:
   - Go to: `http://localhost:8080/admin`
   - Select the new realm (e.g., `tenant_test_abc123`)
   - Go to **Clients** ? Should see `groundup-api` ?

3. Verify client settings:
   - Click `groundup-api`
   - Check **Settings** tab:
     - Client authentication: ON ?
     - Standard flow: ON ?
     - Direct access grants: OFF ?
   - Check **Credentials** tab:
     - Client secret should match `.env` ?
   - Check **Valid redirect URIs**:
     - `http://localhost:5173/*` ?
     - `http://localhost:5000/api/auth/callback` ?

4. Test authentication:
```
http://localhost:8080/realms/tenant_test_abc123/protocol/openid-connect/registrations?client_id=groundup-api&redirect_uri=http://localhost:5000/api/auth/callback&response_type=code&scope=openid
```

Should work without "Invalid redirect_uri" error! ?

---

## Troubleshooting

### **Client creation failed but realm was created**

**Symptoms:**
- Realm exists in Keycloak
- Client does not exist
- Logs show: "Realm created but client creation failed"

**Solution:**
- Check logs for specific error
- Verify `KEYCLOAK_RESOURCE` and `KEYCLOAK_CLIENT_SECRET` are set
- Verify admin token has `manage-clients` permission
- Can manually create client as fallback

### **Redirect URI mismatch**

**Symptoms:**
- "Invalid parameter: redirect_uri" error
- Client exists but wrong URIs configured

**Solution:**
- Check `.env` file has correct URLs
- Restart API to reload environment variables
- Verify client redirect URIs in Keycloak Admin Console
- Update manually if needed: Clients ? groundup-api ? Settings ? Valid Redirect URIs

### **CORS errors in browser**

**Symptoms:**
- "CORS policy: No 'Access-Control-Allow-Origin' header"
- Frontend can't call backend

**Solution:**
- Check client Web Origins in Keycloak
- Should include frontend URL (e.g., `http://localhost:5173`)
- Update manually if needed: Clients ? groundup-api ? Settings ? Web Origins

---

## Migration Guide

### **For Existing Enterprise Tenants**

If you created enterprise tenants **before** this feature was implemented:

1. **Option A: Delete and recreate** (if no data)
   ```bash
   # Delete old tenant
   DELETE FROM Tenants WHERE Id = X;
   
   # Delete old realm in Keycloak Admin Console
   
   # Create new tenant (client will be created automatically)
   ```

2. **Option B: Manually create client** (if tenant has data)
   - Go to Keycloak Admin Console
   - Select the enterprise realm
   - Follow "Manual Fix" instructions from earlier docs
   - No code changes needed - realm will work fine

---

## Future Enhancements

Potential improvements:

1. **Client scopes** - Add custom scopes for fine-grained permissions
2. **Client roles** - Create realm-specific roles automatically
3. **Service accounts** - Enable for backend-to-backend auth
4. **Client mappers** - Add custom claims to tokens
5. **Client policies** - Enforce security policies (PKCE required, etc.)

---

## Related Documentation

- `docs/MANUAL-TESTING-GUIDE.md` - Manual testing procedures
- `docs/PHASE5-IMPLEMENTATION-COMPLETE.md` - Enterprise tenant provisioning
- `GroundUp.infrastructure/services/IdentityProviderAdminService.cs` - Implementation

---

**Status:** ? **IMPLEMENTED & TESTED**  
**Last Updated:** 2025-12-01  
**Version:** 1.0
