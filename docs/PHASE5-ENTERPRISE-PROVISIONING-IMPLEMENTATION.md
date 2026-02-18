# ?? Phase 5: Enterprise Tenant Provisioning - Implementation Guide

## Executive Summary

**Phase 5** implements the enterprise tenant signup flow, including:
- Keycloak realm creation via Admin API
- Enterprise tenant database record
- First admin invitation generation
- Email notification (if email service configured)

---

## ?? What We're Building

### **Enterprise Signup Flow**

```
User fills form at /signup/enterprise
  ?
POST /api/tenants/enterprise/signup
  {
    "companyName": "Acme Corp",
    "contactEmail": "admin@acme.com",
    "contactName": "John Doe",
    "requestedSubdomain": "acme"
  }
  ?
Backend:
  1. Generate unique realm name: "tenant_acmecorp_a3f2"
  2. Create Keycloak realm via Admin API
  3. Enable local authentication in realm
  4. Create Tenant record (TenantType = "enterprise")
  5. Create TenantInvitation (IsAdmin = true)
  6. Send invitation email
  ?
Response:
  {
    "success": true,
    "data": {
      "tenantId": 123,
      "tenantName": "Acme Corp",
      "realmName": "tenant_acmecorp_a3f2",
      "invitationToken": "abc123...",
      "message": "Enterprise tenant created. Invitation sent to admin@acme.com"
    }
  }
```

---

## ?? Implementation Checklist

### **1. DTOs**
- [x] `EnterpriseSignupRequestDto` - Input from frontend
- [x] `EnterpriseSignupResponseDto` - Response to frontend
- [x] `CreateRealmDto` - Keycloak Admin API request

### **2. Keycloak Admin API**
- [x] `IIdentityProviderAdminService.CreateRealmAsync()`
- [x] Realm configuration (local auth enabled)
- [x] Error handling for duplicate realms

### **3. API Endpoint**
- [x] `POST /api/tenants/enterprise/signup`
- [x] Validation (company name, email, etc.)
- [x] Transaction handling
- [x] Error responses

### **4. Email Notification** (Optional)
- [ ] Send invitation email (if email service configured)
- [ ] Email template for enterprise invitation
- [ ] Fallback if email fails (log invitation URL)

---

## ?? Implementation Steps

### **Step 1: Create DTOs**

**File:** `GroundUp.Core/dtos/EnterpriseSignupDtos.cs`

```csharp
namespace GroundUp.Core.dtos
{
    /// <summary>
    /// Request DTO for enterprise tenant signup
    /// </summary>
    public class EnterpriseSignupRequestDto
    {
        /// <summary>
        /// Company/Organization name
        /// Used for tenant name and realm name generation
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Contact email for first admin
        /// REQUIRED - used for invitation
        /// </summary>
        public string ContactEmail { get; set; } = string.Empty;
        
        /// <summary>
        /// Contact name for first admin
        /// </summary>
        public string ContactName { get; set; } = string.Empty;
        
        /// <summary>
        /// Requested subdomain (optional)
        /// If not provided, derived from company name
        /// </summary>
        public string? RequestedSubdomain { get; set; }
        
        /// <summary>
        /// Plan type (default: enterprise-trial)
        /// </summary>
        public string Plan { get; set; } = "enterprise-trial";
    }
    
    /// <summary>
    /// Response DTO for enterprise tenant signup
    /// </summary>
    public class EnterpriseSignupResponseDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string RealmName { get; set; } = string.Empty;
        public string InvitationToken { get; set; } = string.Empty;
        public string InvitationUrl { get; set; } = string.Empty;
        public bool EmailSent { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// DTO for creating a Keycloak realm via Admin API
    /// </summary>
    public class CreateRealmDto
    {
        public string RealmName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool RegistrationAllowed { get; set; } = true;
        public bool RegistrationEmailAsUsername { get; set; } = false;
        public bool LoginWithEmailAllowed { get; set; } = true;
        public bool RememberMe { get; set; } = true;
        public bool VerifyEmail { get; set; } = true;
        public bool ResetPasswordAllowed { get; set; } = true;
        public bool EditUsernameAllowed { get; set; } = false;
    }
}
```

---

### **Step 2: Update IIdentityProviderAdminService**

**File:** `GroundUp.Core/interfaces/IIdentityProviderAdminService.cs`

Add method:
```csharp
/// <summary>
/// Creates a new Keycloak realm
/// Used for enterprise tenant provisioning
/// </summary>
Task<ApiResponse<string>> CreateRealmAsync(CreateRealmDto realmConfig);
```

---

### **Step 3: Implement CreateRealmAsync in IdentityProviderAdminService**

**File:** `GroundUp.infrastructure/services/IdentityProviderAdminService.cs`

```csharp
public async Task<ApiResponse<string>> CreateRealmAsync(CreateRealmDto realmConfig)
{
    try
    {
        var token = await GetAdminTokenAsync();
        
        var realmPayload = new
        {
            realm = realmConfig.RealmName,
            displayName = realmConfig.DisplayName,
            enabled = realmConfig.Enabled,
            registrationAllowed = realmConfig.RegistrationAllowed,
            registrationEmailAsUsername = realmConfig.RegistrationEmailAsUsername,
            loginWithEmailAllowed = realmConfig.LoginWithEmailAllowed,
            rememberMe = realmConfig.RememberMe,
            verifyEmail = realmConfig.VerifyEmail,
            resetPasswordAllowed = realmConfig.ResetPasswordAllowed,
            editUsernameAllowed = realmConfig.EditUsernameAllowed
        };
        
        var content = new StringContent(
            JsonSerializer.Serialize(realmPayload),
            Encoding.UTF8,
            "application/json"
        );
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.PostAsync(
            $"{_config.AuthServerUrl}/admin/realms",
            content
        );
        
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning($"Realm {realmConfig.RealmName} already exists");
            return new ApiResponse<string>(
                string.Empty,
                false,
                "Realm already exists",
                new List<string> { $"Realm '{realmConfig.RealmName}' already exists in Keycloak" },
                StatusCodes.Status409Conflict,
                ErrorCodes.Conflict
            );
        }
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to create realm: {errorContent}");
            return new ApiResponse<string>(
                string.Empty,
                false,
                "Failed to create Keycloak realm",
                new List<string> { errorContent },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError
            );
        }
        
        _logger.LogInformation($"Successfully created Keycloak realm: {realmConfig.RealmName}");
        return new ApiResponse<string>(
            realmConfig.RealmName,
            true,
            "Keycloak realm created successfully"
        );
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error creating Keycloak realm: {ex.Message}", ex);
        return new ApiResponse<string>(
            string.Empty,
            false,
            "Error creating Keycloak realm",
            new List<string> { ex.Message },
            StatusCodes.Status500InternalServerError,
            ErrorCodes.InternalServerError
        );
    }
}
```

---

### **Step 4: Add Enterprise Signup Endpoint to TenantController**

**File:** `GroundUp.api/Controllers/TenantController.cs`

```csharp
/// <summary>
/// Enterprise tenant signup
/// Creates new Keycloak realm, tenant record, and first admin invitation
/// </summary>
[HttpPost("enterprise/signup")]
[AllowAnonymous]
public async Task<ActionResult<ApiResponse<EnterpriseSignupResponseDto>>> EnterpriseSignup(
    [FromBody] EnterpriseSignupRequestDto request)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();
    try
    {
        _logger.LogInformation($"Enterprise signup request for company: {request.CompanyName}");
        
        // 1. Validate request
        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            return BadRequest(new ApiResponse<EnterpriseSignupResponseDto>(
                default!,
                false,
                "Company name is required",
                new List<string> { "CompanyName cannot be empty" },
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationError
            ));
        }
        
        if (string.IsNullOrWhiteSpace(request.ContactEmail))
        {
            return BadRequest(new ApiResponse<EnterpriseSignupResponseDto>(
                default!,
                false,
                "Contact email is required",
                new List<string> { "ContactEmail cannot be empty" },
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationError
            ));
        }
        
        // 2. Generate unique realm name
        var slug = request.RequestedSubdomain ?? 
                   request.CompanyName.ToLowerInvariant()
                       .Replace(" ", "")
                       .Replace(".", "")
                       .Replace("-", "");
        
        var shortGuid = Guid.NewGuid().ToString("N").Substring(0, 4);
        var realmName = $"tenant_{slug}_{shortGuid}";
        
        _logger.LogInformation($"Generated realm name: {realmName}");
        
        // 3. Create Keycloak realm
        var realmConfig = new CreateRealmDto
        {
            RealmName = realmName,
            DisplayName = request.CompanyName,
            Enabled = true,
            RegistrationAllowed = true,
            RegistrationEmailAsUsername = false,
            LoginWithEmailAllowed = true,
            VerifyEmail = true,
            ResetPasswordAllowed = true,
            EditUsernameAllowed = false,
            RememberMe = true
        };
        
        var realmResult = await _identityProviderAdminService.CreateRealmAsync(realmConfig);
        
        if (!realmResult.Success)
        {
            await transaction.RollbackAsync();
            _logger.LogError($"Failed to create Keycloak realm: {realmResult.Message}");
            return StatusCode(realmResult.StatusCode, new ApiResponse<EnterpriseSignupResponseDto>(
                default!,
                false,
                realmResult.Message,
                realmResult.Errors,
                realmResult.StatusCode,
                realmResult.ErrorCode
            ));
        }
        
        // 4. Create Tenant record
        var tenant = new Tenant
        {
            Name = request.CompanyName,
            TenantType = "enterprise",
            KeycloakRealm = realmName,
            Plan = request.Plan,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation($"Created enterprise tenant: {tenant.Name} (ID: {tenant.Id})");
        
        // 5. Create first admin invitation
        var invitationToken = Guid.NewGuid().ToString("N");
        var invitation = new TenantInvitation
        {
            TenantId = tenant.Id,
            InvitationToken = invitationToken,
            ContactEmail = request.ContactEmail,
            ContactName = request.ContactName,
            IsAdmin = true,
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        
        _dbContext.TenantInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync();
        
        await transaction.CommitAsync();
        
        // 6. Generate invitation URL
        var invitationUrl = $"{Request.Scheme}://{Request.Host}/accept-invitation?token={invitationToken}";
        
        // 7. Send email (if email service configured)
        var emailSent = false;
        // TODO: Implement email service
        // await _emailService.SendEnterpriseInvitationAsync(request.ContactEmail, invitationUrl);
        
        _logger.LogInformation($"Enterprise tenant created successfully. Invitation: {invitationUrl}");
        
        var response = new ApiResponse<EnterpriseSignupResponseDto>(
            new EnterpriseSignupResponseDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                RealmName = realmName,
                InvitationToken = invitationToken,
                InvitationUrl = invitationUrl,
                EmailSent = emailSent,
                Message = emailSent 
                    ? $"Enterprise tenant created. Invitation email sent to {request.ContactEmail}" 
                    : $"Enterprise tenant created. Invitation URL: {invitationUrl}"
            },
            true,
            "Enterprise tenant created successfully"
        );
        
        return Ok(response);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError($"Error creating enterprise tenant: {ex.Message}", ex);
        return StatusCode(500, new ApiResponse<EnterpriseSignupResponseDto>(
            default!,
            false,
            "Error creating enterprise tenant",
            new List<string> { ex.Message },
            StatusCodes.Status500InternalServerError,
            ErrorCodes.InternalServerError
        ));
    }
}
```

---

## ?? Testing

### **Manual Testing**

```bash
# Test enterprise signup
curl -X POST http://localhost:5000/api/tenants/enterprise/signup \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Acme Corp",
    "contactEmail": "admin@acme.com",
    "contactName": "John Doe",
    "requestedSubdomain": "acme"
  }'

# Expected response:
{
  "success": true,
  "data": {
    "tenantId": 1,
    "tenantName": "Acme Corp",
    "realmName": "tenant_acme_a3f2",
    "invitationToken": "abc123...",
    "invitationUrl": "http://localhost:5000/accept-invitation?token=abc123...",
    "emailSent": false,
    "message": "Enterprise tenant created..."
  }
}
```

### **Verify in Keycloak**

1. Open http://localhost:8080/admin
2. Login as admin
3. Check realms dropdown - should see `tenant_acme_a3f2`
4. Select the realm
5. Verify settings:
   - Login: Registration allowed ?
   - Login: Email as username ?
   - Login: Verify email ?

### **Verify in Database**

```sql
-- Check tenant created
SELECT * FROM Tenants WHERE TenantType = 'enterprise';

-- Check invitation created
SELECT * FROM TenantInvitations WHERE TenantId = 1;
```

---

## ? Success Criteria

Phase 5 is successful when:
- [ ] `POST /api/tenants/enterprise/signup` endpoint works
- [ ] Keycloak realm created successfully
- [ ] Tenant record created with `TenantType = 'enterprise'`
- [ ] Invitation created with admin privileges
- [ ] Invitation URL returned in response
- [ ] Build compiles successfully
- [ ] No errors in logs

---

## ?? Next Steps

After Phase 5 is complete:
1. Test full enterprise flow end-to-end
2. Move to **Phase 4: Enterprise Bootstrap Security**
3. Implement email service for invitation notifications

---

**Status:** ?? **READY TO IMPLEMENT**  
**Priority:** HIGH  
**Estimated Time:** 1-2 hours  
**Dependencies:** Phase 2 (complete ?)
