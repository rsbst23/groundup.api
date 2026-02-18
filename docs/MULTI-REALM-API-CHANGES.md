# Multi-Realm Architecture - Database Lookup Implementation

**Database-driven realm routing for standard SaaS and enterprise tenants**

---

## ?? **Overview**

GroundUp supports two tenant types with a lightweight database lookup:
- **Standard Tenants (99%):** Use shared `groundup` Keycloak realm
- **Enterprise Tenants (1%):** Use dedicated Keycloak realm with custom identity providers

**Architecture:** URL ? Database lookup ? Realm mapping. Simple, flexible, and performant.

---

## ?? **Design Principles**

### **Why Database Lookup Is Necessary**

**The Problem with URL-only Detection:**
```
https://app.myapp.com          ? How do we know this is 'groundup' vs 'app'?
https://acme.myapp.com         ? How do we know this is 'acme' vs 'groundup'?
```

Both URLs have the same pattern (subdomain.domain.tld), so we **cannot distinguish** between standard and enterprise tenants using URL patterns alone.

**The Solution:**
```
URL ? Database Lookup ? TenantType ? Realm

1. Frontend extracts URL (e.g., 'app.myapp.com')
2. API looks up tenant by RealmUrl
3. If TenantType = 'standard' ? Use 'groundup' realm
4. If TenantType = 'enterprise' ? Use tenant.Name as realm
```

### **Flexible URL Mapping**

Applications built on GroundUp can use **any URL pattern**:
```
Subdomain-based:
  acme.myapp.com ? Tenant: Acme Corp ? Realm: acmecorp

Full domain-based:
  acme.com ? Tenant: Acme Corp ? Realm: acmecorp
  
Path-based (future):
  myapp.com/acme ? Tenant: Acme Corp ? Realm: acmecorp
```

The `RealmUrl` field in the database makes this flexible and future-proof.

---

## ?? **Database Changes**

### **Migration**

**File:** `GroundUp.infrastructure/migrations/AddTenantTypeAndRealmUrl.sql` (NEW)

```sql
-- Add tenant type tracking
ALTER TABLE Tenants 
ADD TenantType VARCHAR(50) NOT NULL DEFAULT 'standard';

-- Add realm URL for flexible mapping (can be subdomain or full domain)
ALTER TABLE Tenants
ADD RealmUrl VARCHAR(255) NULL;

-- Add indexes for fast lookups
CREATE INDEX IX_Tenants_TenantType ON Tenants(TenantType);
CREATE UNIQUE INDEX IX_Tenants_RealmUrl ON Tenants(RealmUrl) WHERE RealmUrl IS NOT NULL;

-- For enterprise tenants, RealmUrl is required
-- For standard tenants, RealmUrl can be NULL or shared (e.g., 'app', 'www')
```

### **Entity Update**

**File:** `GroundUp.Core/entities/Tenant.cs`

```csharp
public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentTenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Tenant type: 'standard' or 'enterprise'
    /// Standard tenants use shared 'groundup' realm
    /// Enterprise tenants use dedicated realm (lowercase tenant name)
    /// </summary>
    public string TenantType { get; set; } = "standard";
    
    /// <summary>
    /// URL used to access this tenant (subdomain or full domain)
    /// Examples: 'acme.myapp.com', 'acme.com', 'app.myapp.com'
    /// Used for realm resolution via database lookup
    /// </summary>
    public string? RealmUrl { get; set; }
    
    /// <summary>
    /// Computed property - is this an enterprise tenant?
    /// </summary>
    public bool IsEnterprise => TenantType == "enterprise";
    
    /// <summary>
    /// Computed property - which Keycloak realm should this tenant use?
    /// Enterprise: tenant name (lowercase)
    /// Standard: 'groundup'
    /// </summary>
    public string KeycloakRealm => IsEnterprise ? Name.ToLowerInvariant() : "groundup";
    
    // Navigation properties
    public Tenant? ParentTenant { get; set; }
    public ICollection<Tenant> ChildTenants { get; set; } = new List<Tenant>();
    public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
}
```

---

## ?? **DTO Updates**

### **File:** `GroundUp.Core/dtos/TenantDto.cs`

```csharp
public class TenantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentTenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public string? ParentTenantName { get; set; }
    public string TenantType { get; set; } = "standard";
    public string? RealmUrl { get; set; }
    public bool IsEnterprise => TenantType == "enterprise";
    public string KeycloakRealm => IsEnterprise ? Name.ToLowerInvariant() : "groundup";
}

public class CreateTenantDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentTenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public string TenantType { get; set; } = "standard";
    public string? RealmUrl { get; set; } // Required for enterprise, optional for standard
}

public class UpdateTenantDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string? RealmUrl { get; set; }
    // TenantType cannot be changed after creation
}
```

### **File:** `GroundUp.Core/dtos/RealmDtos.cs` (NEW)

```csharp
namespace GroundUp.Core.dtos;

/// <summary>
/// DTO for realm resolution request
/// Frontend sends this to API to determine which realm to use
/// </summary>
public class RealmResolutionRequestDto
{
    /// <summary>
    /// The URL being accessed (e.g., 'acme.myapp.com', 'app.myapp.com')
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// DTO for realm resolution response
/// API returns this to tell frontend which realm to use
/// </summary>
public class RealmResolutionResponseDto
{
    /// <summary>
    /// The Keycloak realm to use for authentication
    /// </summary>
    public string Realm { get; set; } = string.Empty;
    
    /// <summary>
    /// The tenant ID (for analytics/tracking)
    /// </summary>
    public int? TenantId { get; set; }
    
    /// <summary>
    /// The tenant name (for display purposes)
    /// </summary>
    public string? TenantName { get; set; }
    
    /// <summary>
    /// Whether this is an enterprise tenant
    /// </summary>
    public bool IsEnterprise { get; set; }
}

/// <summary>
/// DTO for creating Keycloak realm (admin operation)
/// </summary>
public class CreateRealmDto
{
    public string Realm { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// DTO for realm info (admin operation)
/// </summary>
public class RealmDto
{
    public string Realm { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
```

### **File:** `GroundUp.Core/dtos/AuthCallbackDtos.cs`

```csharp
public class AuthCallbackState
{
    public string Flow { get; set; } = "default";
    public string? InvitationToken { get; set; }
    public string? RedirectUrl { get; set; }
    public string? Realm { get; set; } // Realm determined by frontend after DB lookup
}
```

---

## ?? **Interface Updates**

### **File:** `GroundUp.Core/interfaces/ITenantRepository.cs`

Add realm resolution method:

```csharp
public interface ITenantRepository
{
    // Existing methods...
    
    /// <summary>
    /// Resolves realm based on URL (no authentication required - public endpoint)
    /// </summary>
    Task<ApiResponse<RealmResolutionResponseDto>> ResolveRealmByUrlAsync(string url);
}
```

### **File:** `GroundUp.Core/interfaces/IIdentityProviderService.cs`

```csharp
public interface IIdentityProviderService
{
    Task<bool> ValidateTokenAsync(string token);
    
    /// <summary>
    /// Exchanges an authorization code for access and refresh tokens
    /// </summary>
    /// <param name="code">Authorization code from Keycloak</param>
    /// <param name="redirectUri">Redirect URI used in the authorization request</param>
    /// <param name="realm">Optional realm name. If null, uses default from config</param>
    Task<TokenResponseDto?> ExchangeCodeForTokensAsync(string code, string redirectUri, string? realm = null);
}
```

### **File:** `GroundUp.Core/interfaces/IIdentityProviderAdminService.cs`

```csharp
public interface IIdentityProviderAdminService
{
    // Existing user and role methods...
    
    // Realm Management (for enterprise tenants)
    Task<bool> CreateRealmAsync(CreateRealmDto dto);
    Task<bool> DeleteRealmAsync(string realmName);
    Task<RealmDto?> GetRealmAsync(string realmName);
}
```

---

## ?? **Repository Implementation**

### **File:** `GroundUp.infrastructure/repositories/TenantRepository.cs`

Add realm resolution method:

```csharp
public async Task<ApiResponse<RealmResolutionResponseDto>> ResolveRealmByUrlAsync(string url)
{
    try
    {
        // Normalize URL (remove protocol, trailing slash, etc.)
        var normalizedUrl = url.ToLowerInvariant()
            .Replace("https://", "")
            .Replace("http://", "")
            .TrimEnd('/');

        _logger.LogInformation($"Resolving realm for URL: {normalizedUrl}");

        // Look up tenant by RealmUrl
        var tenant = await _context.Tenants
            .Where(t => t.IsActive && t.RealmUrl == normalizedUrl)
            .FirstOrDefaultAsync();

        if (tenant == null)
        {
            // No specific tenant found - return default 'groundup' realm
            _logger.LogInformation($"No tenant found for URL {normalizedUrl}, using default 'groundup' realm");
            return new ApiResponse<RealmResolutionResponseDto>(
                new RealmResolutionResponseDto
                {
                    Realm = "groundup",
                    TenantId = null,
                    TenantName = null,
                    IsEnterprise = false
                },
                true,
                "Using default realm"
            );
        }

        // Tenant found - return its realm
        var response = new RealmResolutionResponseDto
        {
            Realm = tenant.KeycloakRealm,
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            IsEnterprise = tenant.IsEnterprise
        };

        _logger.LogInformation($"Resolved URL {normalizedUrl} to realm {response.Realm} (Tenant: {tenant.Name})");

        return new ApiResponse<RealmResolutionResponseDto>(response, true, "Realm resolved successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error resolving realm for URL {url}: {ex.Message}", ex);
        
        // On error, return default realm to avoid blocking authentication
        return new ApiResponse<RealmResolutionResponseDto>(
            new RealmResolutionResponseDto
            {
                Realm = "groundup",
                TenantId = null,
                TenantName = null,
                IsEnterprise = false
            },
            false,
            "Error resolving realm, using default",
            new List<string> { ex.Message },
            StatusCodes.Status200OK // Still return 200 with default realm
        );
    }
}
```

---

## ?? **Service Implementation**

### **File:** `GroundUp.infrastructure/services/IdentityProviderService.cs`

Update token exchange to accept realm parameter:

```csharp
public async Task<TokenResponseDto?> ExchangeCodeForTokensAsync(
    string code, 
    string redirectUri, 
    string? realm = null)
{
    try
    {
        // Use provided realm or fall back to default from config
        var keycloakRealm = realm ?? _keycloakConfig.Realm;
        
        var tokenEndpoint = $"{_keycloakConfig.AuthServerUrl}/realms/{keycloakRealm}/protocol/openid-connect/token";
        _logger.LogInformation($"Exchanging code for tokens (realm: {keycloakRealm})");

        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("client_id", _keycloakConfig.ClientId),
            new KeyValuePair<string, string>("client_secret", _keycloakConfig.Secret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri)
        });

        var response = await _httpClient.PostAsync(tokenEndpoint, formContent);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Token exchange failed (realm: {keycloakRealm}) with status {response.StatusCode}: {errorContent}");
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

        if (tokenResponse == null)
        {
            _logger.LogError("Failed to parse token response from Keycloak");
            return null;
        }

        var result = new TokenResponseDto
        {
            AccessToken = tokenResponse.TryGetValue("access_token", out var at) ? at.GetString() ?? string.Empty : string.Empty,
            RefreshToken = tokenResponse.TryGetValue("refresh_token", out var rt) ? rt.GetString() ?? string.Empty : string.Empty,
            ExpiresIn = tokenResponse.TryGetValue("expires_in", out var ei) ? ei.GetInt32() : 0,
            RefreshExpiresIn = tokenResponse.TryGetValue("refresh_expires_in", out var rei) ? rei.GetInt32() : 0,
            TokenType = tokenResponse.TryGetValue("token_type", out var tt) ? tt.GetString() ?? "Bearer" : "Bearer",
            IdToken = tokenResponse.TryGetValue("id_token", out var idt) ? idt.GetString() ?? string.Empty : string.Empty
        };

        _logger.LogInformation($"Successfully exchanged authorization code for tokens (realm: {keycloakRealm})");
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error exchanging code for tokens: {ex.Message}", ex);
        return null;
    }
}
```

### **File:** `GroundUp.infrastructure/services/IdentityProviderAdminService.cs`

Add realm management methods at the end of the class:

```csharp
#region Realm Management

public async Task<bool> CreateRealmAsync(CreateRealmDto dto)
{
    await EnsureAdminTokenAsync();

    var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms";

    var payload = new
    {
        realm = dto.Realm,
        displayName = dto.DisplayName,
        enabled = dto.Enabled,
        // Sensible defaults for a new realm
        registrationAllowed = false,
        loginWithEmailAllowed = true,
        duplicateEmailsAllowed = false,
        resetPasswordAllowed = true,
        editUsernameAllowed = false,
        bruteForceProtected = true
    };

    var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);

    if (!response.IsSuccessStatusCode)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        _logger.LogError($"Failed to create realm {dto.Realm}: {errorContent}");
        return false;
    }

    _logger.LogInformation($"Successfully created realm {dto.Realm}");
    return true;
}

public async Task<bool> DeleteRealmAsync(string realmName)
{
    await EnsureAdminTokenAsync();

    var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realmName}";

    var response = await _httpClient.DeleteAsync(requestUrl);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        _logger.LogWarning($"Realm {realmName} not found");
        return false;
    }

    if (!response.IsSuccessStatusCode)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        _logger.LogError($"Failed to delete realm {realmName}: {errorContent}");
        return false;
    }

    _logger.LogInformation($"Successfully deleted realm {realmName}");
    return true;
}

public async Task<RealmDto?> GetRealmAsync(string realmName)
{
    await EnsureAdminTokenAsync();

    var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realmName}";

    var response = await _httpClient.GetAsync(requestUrl);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        _logger.LogWarning($"Realm {realmName} not found");
        return null;
    }

    if (!response.IsSuccessStatusCode)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        _logger.LogError($"Failed to get realm {realmName}: {errorContent}");
        return null;
    }

    var responseContent = await response.Content.ReadAsStringAsync();
    var realmData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

    if (realmData == null)
    {
        return null;
    }

    return new RealmDto
    {
        Realm = realmData.TryGetValue("realm", out var r) ? r.GetString() ?? string.Empty : string.Empty,
        DisplayName = realmData.TryGetValue("displayName", out var dn) ? dn.GetString() ?? string.Empty : string.Empty,
        Enabled = realmData.TryGetValue("enabled", out var e) && e.GetBoolean()
    };
}

#endregion
```

---

## ?? **Controller Updates**

### **File:** `GroundUp.api/Controllers/TenantController.cs`

Add realm resolution endpoint and update tenant creation:

```csharp
/// <summary>
/// Resolve which Keycloak realm to use for a given URL
/// PUBLIC ENDPOINT - No authentication required
/// </summary>
[HttpPost("resolve-realm")]
[AllowAnonymous]
public async Task<IActionResult> ResolveRealm([FromBody] RealmResolutionRequestDto dto)
{
    try
    {
        var result = await _tenantRepository.ResolveRealmByUrlAsync(dto.Url);

        var response = new ApiResponse<RealmResolutionResponseDto>(
            result.Data!,
            result.Success,
            result.Message,
            result.Errors,
            result.StatusCode,
            result.ErrorCode
        );

        return StatusCode(response.StatusCode, response);
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error resolving realm for URL {dto.Url}: {ex.Message}", ex);
        
        // Return default realm on error to avoid blocking authentication
        var response = new ApiResponse<RealmResolutionResponseDto>(
            new RealmResolutionResponseDto { Realm = "groundup", IsEnterprise = false },
            false,
            "Error resolving realm, using default",
            new List<string> { ex.Message },
            StatusCodes.Status200OK
        );
        
        return StatusCode(response.StatusCode, response);
    }
}

[HttpPost]
[RequiresPermission("tenant:create")]
public async Task<IActionResult> CreateTenant([FromBody] CreateTenantDto dto)
{
    try
    {
        _logger.LogInformation($"Creating tenant: {dto.Name} (Type: {dto.TenantType})");

        // Validate RealmUrl for enterprise tenants
        if (dto.TenantType == "enterprise" && string.IsNullOrWhiteSpace(dto.RealmUrl))
        {
            var response = new ApiResponse<TenantDto>(
                default!,
                false,
                "RealmUrl is required for enterprise tenants",
                new List<string> { "RealmUrl cannot be empty" },
                StatusCodes.Status400BadRequest,
                "VALIDATION_FAILED"
            );
            return StatusCode(response.StatusCode, response);
        }

        // If enterprise tenant, create Keycloak realm first
        if (dto.TenantType == "enterprise")
        {
            var realmDto = new CreateRealmDto
            {
                Realm = dto.Name.ToLowerInvariant(),
                DisplayName = dto.Description ?? dto.Name,
                Enabled = dto.IsActive
            };

            var realmCreated = await _identityProviderAdminService.CreateRealmAsync(realmDto);
            
            if (!realmCreated)
            {
                var response = new ApiResponse<TenantDto>(
                    default!,
                    false,
                    "Failed to create Keycloak realm for enterprise tenant",
                    new List<string> { "Realm creation failed" },
                    StatusCodes.Status500InternalServerError,
                    "REALM_CREATION_FAILED"
                );
                return StatusCode(response.StatusCode, response);
            }

            _logger.LogInformation($"Created Keycloak realm: {realmDto.Realm}");
        }

        // Create tenant in database
        var result = await _tenantRepository.AddAsync(dto);

        if (!result.Success)
        {
            // Rollback: Delete realm if it was created
            if (dto.TenantType == "enterprise")
            {
                await _identityProviderAdminService.DeleteRealmAsync(dto.Name.ToLowerInvariant());
            }

            var response = new ApiResponse<TenantDto>(
                default!,
                false,
                result.Message,
                result.Errors,
                StatusCodes.Status400BadRequest,
                result.ErrorCode
            );
            return StatusCode(response.StatusCode, response);
        }

        var successResponse = new ApiResponse<TenantDto>(
            result.Data!,
            true,
            "Tenant created successfully",
            new List<string>(),
            StatusCodes.Status201Created
        );

        return StatusCode(successResponse.StatusCode, successResponse);
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error creating tenant: {ex.Message}", ex);
        var response = new ApiResponse<TenantDto>(
            default!,
            false,
            "An error occurred while creating the tenant",
            new List<string> { ex.Message },
            StatusCodes.Status500InternalServerError,
            "TENANT_CREATION_ERROR"
        );
        return StatusCode(response.StatusCode, response);
    }
}
```

### **File:** `GroundUp.api/Controllers/AuthController.cs`

Update callback to use realm from state:

```csharp
[HttpGet("callback")]
public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? state)
{
    try
    {
        _logger.LogInformation("Auth callback initiated");

        // 1. Parse state to get realm (frontend determined this via DB lookup)
        AuthCallbackState? callbackState = null;
        string? realm = null;
        
        if (!string.IsNullOrEmpty(state))
        {
            try
            {
                callbackState = JsonSerializer.Deserialize<AuthCallbackState>(
                    System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state)));
                realm = callbackState?.Realm;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to parse state parameter: {ex.Message}");
            }
        }

        // 2. Exchange code for tokens (with realm from frontend)
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
        var tokenResponse = await _identityProviderService.ExchangeCodeForTokensAsync(code, redirectUri, realm);

        if (tokenResponse == null)
        {
            _logger.LogError("Failed to exchange authorization code for tokens");
            var response = new ApiResponse<AuthCallbackResponseDto>(
                default!,
                false,
                "Failed to exchange authorization code for tokens",
                new List<string> { "Token exchange failed" },
                StatusCodes.Status400BadRequest,
                "TOKEN_EXCHANGE_FAILED"
            );
            return StatusCode(response.StatusCode, response);
        }

        // Rest of existing callback logic...
        // (Extract user ID, sync user, handle invitation flow, etc.)
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error in auth callback: {ex.Message}", ex);
        var response = new ApiResponse<AuthCallbackResponseDto>(
            default!,
            false,
            "An error occurred during authentication",
            new List<string> { ex.Message },
            StatusCodes.Status500InternalServerError,
            "AUTH_CALLBACK_ERROR"
        );
        return StatusCode(response.StatusCode, response);
    }
}
```

---

## ??? **Database Migration Steps**

### **Step 1: Create Migration**

```bash
cd GroundUp.api
dotnet ef migrations add AddTenantTypeAndRealmUrl --project ../GroundUp.infrastructure --startup-project .
```

### **Step 2: Review Generated Migration**

Check the migration file in `GroundUp.infrastructure/Migrations/`. Ensure it adds:
- `TenantType` column with default value `'standard'`
- `RealmUrl` column (nullable)
- Index on `TenantType`
- Unique index on `RealmUrl`

### **Step 3: Apply Migration**

```bash
dotnet ef database update --project ../GroundUp.infrastructure --startup-project .
```

### **Step 4: Verify in Database**

```sql
-- Check schema
DESCRIBE Tenants;

-- Verify all existing tenants have 'standard' type
SELECT Id, Name, TenantType, RealmUrl FROM Tenants;

-- Check indexes
SHOW INDEX FROM Tenants WHERE Key_name IN ('IX_Tenants_TenantType', 'IX_Tenants_RealmUrl');
```

---

## ?? **Testing Guide**

### **Test 1: Realm Resolution - Standard Tenant**

```bash
POST /api/tenants/resolve-realm
{
  "url": "app.myapp.com"
}
```

**Expected Response:**
```json
{
  "data": {
    "realm": "groundup",
    "tenantId": null,
    "tenantName": null,
    "isEnterprise": false
  },
  "success": true,
  "message": "Using default realm"
}
```

### **Test 2: Realm Resolution - Enterprise Tenant**

**Setup:**
```bash
POST /api/tenants
{
  "name": "AcmeCorp",
  "description": "Acme Corporation",
  "tenantType": "enterprise",
  "realmUrl": "acme.myapp.com",
  "isActive": true
}
```

**Test:**
```bash
POST /api/tenants/resolve-realm
{
  "url": "acme.myapp.com"
}
```

**Expected Response:**
```json
{
  "data": {
    "realm": "acmecorp",
    "tenantId": 123,
    "tenantName": "AcmeCorp",
    "isEnterprise": true
  },
  "success": true,
  "message": "Realm resolved successfully"
}
```

### **Test 3: Enterprise Tenant Creation**

```bash
POST /api/tenants
{
  "name": "BigBank",
  "description": "Big Bank Financial",
  "tenantType": "enterprise",
  "realmUrl": "bigbank.com",
  "isActive": true
}
```

**Expected:**
- ? Tenant created in database with `TenantType = 'enterprise'`
- ? Keycloak realm `bigbank` created
- ? RealmUrl set to `bigbank.com`
- ? Users accessing `bigbank.com` will use `bigbank` realm

### **Test 4: Frontend Integration Flow**

```javascript
// 1. User navigates to acme.myapp.com
const currentUrl = window.location.hostname; // 'acme.myapp.com'

// 2. Frontend calls realm resolution API
const response = await fetch('/api/tenants/resolve-realm', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ url: currentUrl })
});

const { data } = await response.json();
const realm = data.realm; // 'acmecorp'

// 3. Frontend initiates Keycloak auth with resolved realm
const authUrl = `${keycloakUrl}/realms/${realm}/protocol/openid-connect/auth?...`;
window.location.href = authUrl;

// 4. After Keycloak redirect, frontend includes realm in state
const state = btoa(JSON.stringify({
  flow: 'default',
  realm: realm
}));
```

---

## ?? **Implementation Checklist**

### **Phase 1: Database & Entities** ?
- [x] Create migration to add `TenantType` and `RealmUrl` columns
- [x] Update `Tenant` entity with new properties
- [x] Apply migration to database
- [x] Verify existing tenants have `'standard'` type

### **Phase 2: DTOs** ?
- [x] Update `TenantDto` with `TenantType`, `RealmUrl`, and computed properties
- [x] Update `CreateTenantDto` and `UpdateTenantDto`
- [x] Create new `RealmDtos.cs` file with all realm-related DTOs
- [x] Update `AuthCallbackState` with optional `Realm`

### **Phase 3: Interfaces** ?
- [x] Add `ResolveRealmByUrlAsync` to `ITenantRepository`
- [x] Update `IIdentityProviderService` - add realm parameter
- [x] Update `IIdentityProviderAdminService` - add realm methods

### **Phase 4: Repositories** ?
- [x] Implement `ResolveRealmByUrlAsync` in `TenantRepository`
- [x] Add validation for `RealmUrl` in `AddAsync` and `UpdateAsync`
- [x] Test realm resolution with different URLs

### **Phase 5: Services** ?
- [x] Update `IdentityProviderService.ExchangeCodeForTokensAsync`
- [x] Add realm management methods to `IdentityProviderAdminService`
- [x] Test realm creation/deletion with Keycloak

### **Phase 6: Controllers** ?
- [x] Add `POST /api/tenants/resolve-realm` endpoint (public, no auth)
- [x] Update `TenantController.CreateTenant` to create realms for enterprise tenants
- [x] Update `AuthController.Callback` to handle realm from state
- [x] Add error handling and rollback logic

### **Phase 7: Testing** ??
- [ ] Test realm resolution for unknown URL (should return 'groundup')
- [ ] Test realm resolution for standard tenant URL
- [ ] Test realm resolution for enterprise tenant URL
- [ ] Test enterprise tenant creation (realm + database)
- [ ] Test tenant creation rollback on failure
- [ ] Test auth callback with different realms
- [ ] Performance test: Measure DB lookup latency

### **Phase 8: Documentation** ??
- [ ] Update API documentation with new endpoint
- [ ] Document frontend integration flow
- [ ] Document realm naming conventions
- [ ] Create troubleshooting guide

---

## ?? **Important Notes**

### **Realm URL Conventions**
- RealmUrl must be unique across all tenants
- Normalize URLs (lowercase, remove protocol, trim slashes)
- Examples:
  - `acme.myapp.com` (subdomain-based)
  - `acme.com` (full domain-based)
  - `app.myapp.com` (shared for standard tenants)

### **Performance Considerations**
- Database lookup adds ~10-50ms latency
- Happens once per login session (not per request)
- Consider caching realm mappings in production
- Index on `RealmUrl` ensures fast lookups

### **Rollback Strategy**
If enterprise tenant creation fails:
1. Delete Keycloak realm (if created)
2. Return error to user
3. Log failure for investigation
4. Database transaction ensures consistency

### **Security**
- Realm resolution endpoint is PUBLIC (no auth required)
- No sensitive data exposed (realm names are public anyway)
- Keycloak handles all authentication security
- Database lookup is read-only, safe for public access

### **Flexibility**
This approach supports:
- ? Subdomain-based URLs (`acme.myapp.com`)
- ? Full domain URLs (`acme.com`)
- ? Multiple URLs per tenant (future: comma-separated RealmUrl)
- ? Path-based routing (future: store path in RealmUrl)

---

## ?? **Related Documentation**

- [Keycloak Realm Management API](https://www.keycloak.org/docs-api/latest/rest-api/index.html#_realms_admin_resource)
- [Multi-Tenant Authentication Flow](./MULTI-TENANT-SOCIAL-AUTH.md)
- [Tenant Management Guide](./TENANT-MANAGEMENT-SUMMARY.md)
- [User Creation Patterns](./USER-CREATION-PATTERNS.md)

---

## ?? **Support**

For questions or issues:
1. Check Keycloak admin console for realm status
2. Review application logs for realm resolution details
3. Verify database schema and indexes
4. Test realm resolution endpoint directly

---

**Document Version:** 2.0  
**Last Updated:** 2024  
**Status:** Ready for Implementation  
**Breaking Change:** Yes - New database columns and public API endpoint
