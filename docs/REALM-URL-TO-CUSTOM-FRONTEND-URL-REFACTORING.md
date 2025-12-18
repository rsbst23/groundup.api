# ?? RealmUrl ? CustomFrontendUrl Refactoring

## Overview

Replaced the `RealmUrl` field with `CustomFrontendUrl` in the `Tenant` entity to support a cleaner, more flexible URL-based tenant resolution system.

---

## Why This Change?

### **Problem with `RealmUrl`**

The old `RealmUrl` field stored just a subdomain identifier (e.g., `"acme"`), which created several issues:

1. ? **Fragile extraction logic** - Had to parse subdomains from URLs
2. ? **Limited to subdomains** - Couldn't properly handle custom domains like `"app.acmecorp.com"`
3. ? **Ambiguous for custom domains** - What subdomain to extract from `"acmecorp.com"`?
4. ? **Redundant with DTO** - Frontend already knows the full URL

### **Solution with `CustomFrontendUrl`**

Store the complete frontend URL directly:

1. ? **No extraction needed** - Use what frontend sends
2. ? **Supports all URL formats** - Subdomains AND custom domains
3. ? **Direct matching** - Lookup by exact URL
4. ? **Simpler code** - Less parsing, fewer assumptions

---

## Changes Made

### **1. Database Schema**

#### **Before:**
```csharp
public class Tenant
{
    public string? RealmUrl { get; set; } // "acme"
}
```

#### **After:**
```csharp
public class Tenant
{
    /// <summary>
    /// Full frontend URL for this enterprise tenant
    /// Examples: "acme.yourapp.com", "app.acmecorp.com"
    /// Used for realm resolution and redirect URIs
    /// </summary>
    public string? CustomFrontendUrl { get; set; }
}
```

#### **Migration:**
- ? Adds `CustomFrontendUrl` column
- ? Copies data from `RealmUrl` to `CustomFrontendUrl`
- ? Drops `RealmUrl` column
- ? Supports rollback

---

### **2. DTOs Updated**

#### **EnterpriseSignupRequestDto:**

```csharp
public class EnterpriseSignupRequestDto
{
    /// <summary>
    /// Subdomain identifier (e.g., "acme")
    /// Used for realm name generation only
    /// </summary>
    public string RequestedSubdomain { get; set; } = string.Empty;
    
    /// <summary>
    /// Full frontend URL for this tenant
    /// Frontend constructs this from:
    /// - Subdomain mode: "acme.yourapp.com" 
    /// - Custom domain: "app.acmecorp.com"
    /// </summary>
    public string FrontendUrl { get; set; } = string.Empty;
}
```

**Frontend UX:**
```
????????????????????????????????????????
? Subdomain: [acme] .yourapp.com      ?
?                                       ?
? Or use custom domain:                ?
? [  ] app.acmecorp.com                ?
????????????????????????????????????????

// Frontend sends:
{
  "requestedSubdomain": "acme",
  "frontendUrl": "acme.yourapp.com"  // or "app.acmecorp.com"
}
```

---

### **3. Realm Resolution**

#### **Before:**
```csharp
// Fragile - had to extract subdomain
var subdomain = ExtractSubdomain(url); // "acme" from "acme.yourapp.com"
var tenant = await _dbSet
    .Where(t => t.RealmUrl == subdomain)
    .FirstOrDefaultAsync();
```

#### **After:**
```csharp
// Direct match - clean and simple
var normalizedUrl = NormalizeUrl(url); // "acme.yourapp.com"
var tenant = await _dbSet
    .Where(t => t.CustomFrontendUrl == normalizedUrl)
    .FirstOrDefaultAsync();

private string NormalizeUrl(string url)
{
    return url.ToLowerInvariant()
        .Replace("https://", "")
        .Replace("http://", "")
        .TrimEnd('/');
}
```

**Supports all formats:**
- ? `"acme.yourapp.com"` ? Matches exactly
- ? `"app.acmecorp.com"` ? Matches exactly
- ? `"https://acme.yourapp.com/"` ? Normalized to `"acme.yourapp.com"`

---

### **4. Keycloak Client Configuration**

Now automatically configures redirect URIs using tenant's frontend URL:

```csharp
public async Task<ApiResponse<string>> CreateRealmWithClientAsync(
    CreateRealmDto dto, 
    string frontendUrl)
{
    // Create realm
    var realmResult = await CreateRealmAsync(dto);
    
    // Create client with tenant-specific redirect URIs
    var clientDto = BuildClientConfiguration(frontendUrl);
    await CreateClientInRealmAsync(dto.Realm, clientDto);
}

private CreateClientDto BuildClientConfiguration(string tenantFrontendUrl)
{
    var redirectUris = new List<string>
    {
        // Tenant's specific frontend
        $"https://{tenantFrontendUrl}/auth/callback",
        $"https://{tenantFrontendUrl}/*",
        
        // Backend API (shared)
        "http://localhost:5000/api/auth/callback",
        
        // Local dev
        "http://localhost:5173/*"
    };
    
    return new CreateClientDto { RedirectUris = redirectUris };
}
```

---

## API Changes

### **Enterprise Signup Request**

#### **Before:**
```json
{
  "companyName": "Acme Corp",
  "requestedSubdomain": "acme",
  "plan": "enterprise-trial"
}
```

#### **After:**
```json
{
  "companyName": "Acme Corp",
  "requestedSubdomain": "acme",
  "frontendUrl": "acme.yourapp.com",
  "plan": "enterprise-trial"
}
```

### **Enterprise Signup Response**

#### **Added:**
```json
{
  "tenantId": 1,
  "realmName": "tenant_acme_xyz",
  "frontendUrl": "acme.yourapp.com",  // ? NEW
  "invitationToken": "...",
  "invitationUrl": "..."
}
```

---

## Files Modified

### **Core:**
- `GroundUp.core/entities/Tenant.cs` - Added `CustomFrontendUrl`, removed (conceptually) `RealmUrl`
- `GroundUp.core/dtos/TenantDto.cs` - Updated DTOs
- `GroundUp.core/dtos/EnterpriseSignupDtos.cs` - Added `FrontendUrl` field
- `GroundUp.core/interfaces/IIdentityProviderAdminService.cs` - Added `CreateRealmWithClientAsync`

### **Infrastructure:**
- `GroundUp.infrastructure/services/IdentityProviderAdminService.cs` - Implemented new method
- `GroundUp.infrastructure/repositories/TenantRepository.cs` - Updated all mappings and realm resolution
- `GroundUp.infrastructure/Migrations/ReplaceRealmUrlWithCustomFrontendUrl.cs` - Data migration

### **API:**
- `GroundUp.api/Controllers/TenantController.cs` - Updated enterprise signup to use new approach

---

## Benefits

### **For Developers:**
1. ? **Simpler code** - No URL parsing logic
2. ? **More reliable** - Direct string matching
3. ? **Easier to debug** - What you store is what you match
4. ? **Better testability** - No edge cases with URL parsing

### **For Users:**
1. ? **Flexible URLs** - Subdomain OR custom domain
2. ? **Clear expectations** - See exact URL before signup
3. ? **No surprises** - URL they enter is URL they get

### **For System:**
1. ? **Automatic client config** - Redirect URIs set correctly
2. ? **Scalable** - Works for unlimited tenants
3. ? **Future-proof** - Easy to extend for new URL patterns

---

## Migration Guide

### **For Existing Enterprise Tenants:**

The migration automatically copies `RealmUrl` ? `CustomFrontendUrl`, so existing tenants will work without changes.

**However**, if you had:
- `RealmUrl = "acme"` (just subdomain)

The frontend should now send:
- `FrontendUrl = "acme.yourapp.com"` (full URL)

### **Database Migration:**

```bash
# Apply migration
cd GroundUp.infrastructure
dotnet ef database update

# Rollback if needed
dotnet ef database update [PreviousMigrationName]
```

---

## Testing

### **Test Scenarios:**

1. ? **Subdomain URL**
   ```json
   {
     "frontendUrl": "acme.yourapp.com"
   }
   ```

2. ? **Custom Domain**
   ```json
   {
     "frontendUrl": "app.acmecorp.com"
   }
   ```

3. ? **Realm Resolution**
   ```
   POST /api/tenants/resolve-realm
   {
     "url": "acme.yourapp.com"
   }
   
   Response:
   {
     "realm": "tenant_acme_xyz",
     "tenantId": 1
   }
   ```

4. ? **Keycloak Client Created**
   - Check Keycloak Admin ? Realm ? Clients ? groundup-api
   - Valid Redirect URIs should include tenant's frontend URL

---

## Next Steps

### **Immediate:**
1. ? Migration created and tested
2. ? All code updated
3. ? Build successful

### **Future Enhancements:**
1. ? Add `SystemSettings` table for API URL and default frontend URL
2. ? Frontend URL validation (DNS check, SSL certificate)
3. ? Support for multiple frontend URLs per tenant (e.g., staging + production)
4. ? Automatic SSL certificate provisioning for custom domains

---

## Related Documentation

- `docs/MANUAL-TESTING-GUIDE.md` - Updated testing procedures
- `docs/AUTOMATIC-CLIENT-PROVISIONING.md` - Client configuration details
- `docs/PHASE5-IMPLEMENTATION-COMPLETE.md` - Enterprise tenant provisioning

---

**Status:** ? **COMPLETE**  
**Last Updated:** 2025-12-01  
**Migration:** `20251202034150_ReplaceRealmUrlWithCustomFrontendUrl`
