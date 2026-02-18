# Enterprise SSO Invitation Implementation Guide

## Overview

This document provides implementation guidance for enterprise SSO invitation flows with flexible authentication provider policies and domain-based auto-join capabilities.

---

## 1. Database Schema Changes

### 1.1 Add Columns to `Tenant` Table

```sql
-- Add SSO policy and domain allowlist columns
ALTER TABLE Tenants
ADD SsoAutoJoinDomains NVARCHAR(MAX) NULL,  -- JSON array of allowed domains
    SsoAutoJoinRoleId INT NULL,             -- Default role for auto-join
    AuthProviderPolicy INT NOT NULL DEFAULT 0; -- 0 = EnterpriseOnly, 1 = EnterprisePlusSocial, 2 = Unrestricted

-- Add foreign key for auto-join role
ALTER TABLE Tenants
ADD CONSTRAINT FK_Tenants_SsoAutoJoinRoleId 
    FOREIGN KEY (SsoAutoJoinRoleId) REFERENCES Roles(Id);
```

### 1.2 Migration File

Create: `GroundUp.infrastructure/Migrations/YYYYMMDDHHMMSS_AddSsoAutoJoinToTenants.cs`

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddSsoAutoJoinToTenants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SsoAutoJoinDomains",
            table: "Tenants",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SsoAutoJoinRoleId",
            table: "Tenants",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AuthProviderPolicy",
            table: "Tenants",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_Tenants_SsoAutoJoinRoleId",
            table: "Tenants",
            column: "SsoAutoJoinRoleId");

        migrationBuilder.AddForeignKey(
            name: "FK_Tenants_Roles_SsoAutoJoinRoleId",
            table: "Tenants",
            column: "SsoAutoJoinRoleId",
            principalTable: "Roles",
            principalColumn: "Id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Tenants_Roles_SsoAutoJoinRoleId",
            table: "Tenants");

        migrationBuilder.DropIndex(
            name: "IX_Tenants_SsoAutoJoinRoleId",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "SsoAutoJoinDomains",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "SsoAutoJoinRoleId",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "AuthProviderPolicy",
            table: "Tenants");
    }
}
```

---

## 2. Core Entity Updates

### 2.1 Add Enum: `AuthProviderPolicy`

File: `GroundUp.Core/enums/TenantEnums.cs`

```csharp
namespace GroundUp.Core.enums
{
    /// <summary>
    /// Defines which authentication providers are allowed for a tenant
    /// </summary>
    public enum AuthProviderPolicy
    {
        /// <summary>
        /// Only enterprise SSO (Azure AD, Okta, Google Workspace)
        /// Recommended for enterprise tenants
        /// Requires verified email from enterprise IdP
        /// </summary>
        EnterpriseOnly = 0,
        
        /// <summary>
        /// Enterprise SSO + verified social providers
        /// Requires email claim and email_verified = true
        /// Use for mixed workforce (employees + contractors)
        /// </summary>
        EnterprisePlusSocial = 1,
        
        /// <summary>
        /// All providers including unverified social auth
        /// NOT recommended for enterprise security
        /// Use only for development/testing
        /// </summary>
        Unrestricted = 2
    }
}
```

### 2.2 Update `Tenant` Entity

File: `GroundUp.Core/entities/Tenant.cs`

Add these properties:

```csharp
using System.Text.Json;

public class Tenant
{
    // ...existing properties...
    
    /// <summary>
    /// JSON array of email domains allowed for SSO auto-join
    /// Example: ["acme.com", "acmecorp.com"]
    /// If null/empty: All users require explicit invitation
    /// If set: Users from these domains can auto-join on first SSO login
    /// </summary>
    public string? SsoAutoJoinDomainsJson { get; set; }
    
    /// <summary>
    /// Parsed list of allowed domains (not mapped to database)
    /// </summary>
    [NotMapped]
    public List<string>? SsoAutoJoinDomains
    {
        get => string.IsNullOrEmpty(SsoAutoJoinDomainsJson)
            ? null
            : JsonSerializer.Deserialize<List<string>>(SsoAutoJoinDomainsJson);
        set => SsoAutoJoinDomainsJson = value == null
            ? null
            : JsonSerializer.Serialize(value);
    }
    
    /// <summary>
    /// Default role ID assigned when users auto-join via allowed domain
    /// If null: Uses tenant's default member role
    /// </summary>
    public int? SsoAutoJoinRoleId { get; set; }
    
    /// <summary>
    /// Navigation property for auto-join role
    /// </summary>
    public Role? SsoAutoJoinRole { get; set; }
    
    /// <summary>
    /// Authentication provider policy for this tenant
    /// Controls which Keycloak identity providers are allowed
    /// </summary>
    public AuthProviderPolicy AuthProviderPolicy { get; set; } = AuthProviderPolicy.EnterpriseOnly;
}
```

### 2.3 Update `ApplicationDbContext`

File: `GroundUp.infrastructure/data/ApplicationDbContext.cs`

Add to `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ...existing configurations...
    
    // Tenant SSO configuration
    modelBuilder.Entity<Tenant>()
        .HasOne(t => t.SsoAutoJoinRole)
        .WithMany()
        .HasForeignKey(t => t.SsoAutoJoinRoleId)
        .OnDelete(DeleteBehavior.SetNull);
    
    modelBuilder.Entity<Tenant>()
        .Property(t => t.SsoAutoJoinDomainsJson)
        .HasColumnName("SsoAutoJoinDomains");
}
```

---

## 3. DTOs

### 3.1 Update `TenantDto`

File: `GroundUp.Core/dtos/TenantDto.cs`

```csharp
public class TenantDto
{
    // ...existing properties...
    
    public List<string>? SsoAutoJoinDomains { get; set; }
    public int? SsoAutoJoinRoleId { get; set; }
    public string? SsoAutoJoinRoleName { get; set; }
    public AuthProviderPolicy AuthProviderPolicy { get; set; }
}
```

### 3.2 Update `CreateTenantDto` and `UpdateTenantDto`

Add these properties to both:

```csharp
public List<string>? SsoAutoJoinDomains { get; set; }
public int? SsoAutoJoinRoleId { get; set; }
public AuthProviderPolicy? AuthProviderPolicy { get; set; }
```

### 3.3 Update AutoMapper Profile

File: `GroundUp.infrastructure/mappings/MappingProfile.cs`

```csharp
CreateMap<Tenant, TenantDto>()
    .ForMember(dest => dest.SsoAutoJoinDomains, opt => opt.MapFrom(src => src.SsoAutoJoinDomains))
    .ForMember(dest => dest.SsoAutoJoinRoleName, opt => opt.MapFrom(src => src.SsoAutoJoinRole != null ? src.SsoAutoJoinRole.Name : null));

CreateMap<CreateTenantDto, Tenant>()
    .ForMember(dest => dest.SsoAutoJoinDomains, opt => opt.MapFrom(src => src.SsoAutoJoinDomains));

CreateMap<UpdateTenantDto, Tenant>()
    .ForMember(dest => dest.SsoAutoJoinDomains, opt => opt.MapFrom(src => src.SsoAutoJoinDomains))
    .ForMember(dest => dest.AuthProviderPolicy, opt => opt.MapFrom(src => src.AuthProviderPolicy ?? AuthProviderPolicy.EnterpriseOnly));
```

---

## 4. Core Implementation: Authorization Logic

### 4.1 Add Helper Method to `AuthController`

File: `GroundUp.api/Controllers/AuthController.cs`

Add this private method:

```csharp
/// <summary>
/// Validates SSO user and handles auto-join or invitation acceptance for enterprise tenants
/// Returns true if user was authorized and assigned to tenant
/// Returns false if unauthorized (caller should delete Keycloak user and return error)
/// </summary>
private async Task<(bool authorized, string? errorMessage)> ValidateAndAssignSsoUserAsync(
    Guid userId,
    string keycloakUserId,
    string userEmail,
    Tenant tenant,
    string realm)
{
    _logger.LogInformation($"Validating SSO user {userEmail} for enterprise tenant {tenant.Name}");
    
    // 1. Validate email is present (required for enterprise)
    if (string.IsNullOrEmpty(userEmail))
    {
        _logger.LogWarning($"SSO login without email for realm {realm}");
        return (false, "Your authentication provider did not share your email address. Please use your organization's SSO or request a direct invitation.");
    }
    
    // 2. Validate email verification based on auth provider policy
    var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
    
    if (tenant.AuthProviderPolicy == AuthProviderPolicy.EnterpriseOnly && !keycloakUser.EmailVerified)
    {
        _logger.LogWarning($"Unverified email login for strict enterprise realm {realm}");
        return (false, "Email verification required. Please use your organization's SSO provider.");
    }
    
    // 3. Extract domain from email
    var userDomain = userEmail.Split('@')[1].ToLowerInvariant();
    
    // 4. Check auto-join via allowed domains
    if (tenant.SsoAutoJoinDomains?.Contains(userDomain) == true)
    {
        _logger.LogInformation($"Auto-joining user {userId} from allowed domain {userDomain}");
        
        // Assign to tenant
        await _userTenantRepository.AssignUserToTenantAsync(
            userId, 
            tenant.Id, 
            isAdmin: false, 
            keycloakUserId
        );
        
        // Assign default role
        var roleId = tenant.SsoAutoJoinRoleId;
        
        if (roleId == null)
        {
            // Fallback to default "Member" role
            var defaultRole = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.TenantId == tenant.Id && r.Name == "Member");
            
            roleId = defaultRole?.Id;
        }
        
        if (roleId != null)
        {
            await _dbContext.UserRoles.AddAsync(new UserRole
            {
                UserId = userId,
                RoleId = roleId.Value,
                TenantId = tenant.Id
            });
            
            await _dbContext.SaveChangesAsync();
        }
        
        _logger.LogInformation($"User {userId} auto-joined tenant {tenant.Id} with role {roleId}");
        return (true, null);
    }
    
    // 5. Check for pending invitation
    var pendingInvitations = await _tenantInvitationRepository
        .GetInvitationsForEmailAsync(userEmail);
    
    var tenantInvitation = pendingInvitations.Data
        ?.FirstOrDefault(i => i.TenantId == tenant.Id && i.Status == InvitationStatus.Pending);
    
    if (tenantInvitation != null)
    {
        _logger.LogInformation($"Auto-accepting invitation for user {userId}");
        
        // Accept the invitation (creates UserTenant and assigns roles)
        var acceptResult = await _tenantInvitationRepository.AcceptInvitationAsync(
            tenantInvitation.InvitationToken,
            userId,
            keycloakUserId
        );
        
        if (acceptResult.Success)
        {
            _logger.LogInformation($"User {userId} accepted invitation to tenant {tenant.Id}");
            return (true, null);
        }
        else
        {
            _logger.LogError($"Failed to accept invitation: {acceptResult.Message}");
            return (false, $"Failed to process invitation: {acceptResult.Message}");
        }
    }
    
    // 6. No authorization found
    _logger.LogWarning($"Unauthorized SSO login attempt for {userEmail} in tenant {tenant.Name}");
    return (false, "Access denied. Please request an invitation from your administrator.");
}
```

### 4.2 Update `HandleDefaultFlowAsync`

Modify the existing method to use the new validation logic:

```csharp
private async Task<AuthCallbackResponseDto> HandleDefaultFlowAsync(
    Guid userId, 
    string keycloakUserId, 
    string realm, 
    string accessToken)
{
    try
    {
        _logger.LogInformation($"Processing default flow for Keycloak user {keycloakUserId} in realm {realm}");

        // Get Keycloak user details
        var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
        if (keycloakUser == null)
        {
            return new AuthCallbackResponseDto
            {
                Success = false,
                Flow = "default",
                RequiresTenantSelection = false,
                ErrorMessage = "User not found in Keycloak"
            };
        }

        // Check if user exists in local DB, if not create them
        var existingUser = await _dbContext.Users.FindAsync(userId);
        
        if (existingUser == null)
        {
            // First time login - create user
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var newUser = new core.entities.User
                {
                    Id = userId,
                    DisplayName = !string.IsNullOrEmpty(keycloakUser.FirstName) 
                        ? $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim() 
                        : keycloakUser.Username ?? "Unknown",
                    Email = keycloakUser.Email,
                    Username = keycloakUser.Username,
                    FirstName = keycloakUser.FirstName,
                    LastName = keycloakUser.LastName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Users.Add(newUser);
                await _dbContext.SaveChangesAsync();

                await transaction.CommitAsync();
                _logger.LogInformation($"Created new user {userId} for realm {realm}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Resolve tenant memberships
        var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);

        // Handle users with no tenant memberships
        if (userTenants.Count == 0)
        {
            // Determine tenant from realm
            var tenant = await _dbContext.Tenants
                .Include(t => t.SsoAutoJoinRole)
                .FirstOrDefaultAsync(t => t.RealmName == realm);
            
            if (tenant == null)
            {
                _logger.LogError($"No tenant found for realm {realm}");
                return new AuthCallbackResponseDto
                {
                    Success = false,
                    Flow = "default",
                    RequiresTenantSelection = false,
                    ErrorMessage = "Tenant configuration error"
                };
            }
            
            if (tenant.TenantType == TenantType.Enterprise)
            {
                // ENTERPRISE TENANT: Validate and potentially auto-join
                var (authorized, errorMessage) = await ValidateAndAssignSsoUserAsync(
                    userId,
                    keycloakUserId,
                    keycloakUser.Email,
                    tenant,
                    realm
                );
                
                if (!authorized)
                {
                    // Delete Keycloak user (cleanup orphaned account)
                    await _identityProviderAdminService.DeleteUserAsync(keycloakUserId, realm);
                    
                    return new AuthCallbackResponseDto
                    {
                        Success = false,
                        Flow = "unauthorized_sso_access",
                        RequiresTenantSelection = false,
                        ErrorMessage = errorMessage
                    };
                }
                
                // User was authorized and assigned - refresh memberships
                userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);
            }
            else
            {
                // STANDARD TENANT: Auto-create tenant for them (existing new_org flow)
                return await HandleNewOrganizationFlowAsync(userId, keycloakUserId, realm, accessToken);
            }
        }

        // At this point, userTenants.Count should be > 0
        if (userTenants.Count == 0)
        {
            // This should only happen if something went wrong
            return new AuthCallbackResponseDto
            {
                Success = false,
                Flow = "default",
                RequiresTenantSelection = false,
                ErrorMessage = "Unable to assign user to tenant. Please contact support."
            };
        }

        // Continue with normal tenant selection logic
        if (userTenants.Count == 1)
        {
            // Auto-select single tenant
            var customToken = await _tokenService.GenerateTokenAsync(
                userId,
                userTenants[0].TenantId,
                ExtractClaims(accessToken)
            );

            SetAuthCookie(customToken);

            _logger.LogInformation($"User {userId} auto-assigned to tenant {userTenants[0].TenantId}");
            
            return new AuthCallbackResponseDto
            {
                Success = true,
                Flow = "default",
                Token = customToken,
                TenantId = userTenants[0].TenantId,
                TenantName = userTenants[0].Tenant?.Name,
                RequiresTenantSelection = false,
                Message = "User authenticated successfully"
            };
        }
        else
        {
            // Multiple tenants - return list for selection
            _logger.LogInformation($"User {userId} has {userTenants.Count} tenants - requires tenant selection");
            
            // Store Keycloak token temporarily for tenant selection
            SetAuthCookie(accessToken, "KeycloakToken");
            
            return new AuthCallbackResponseDto
            {
                Success = true,
                Flow = "default",
                RequiresTenantSelection = true,
                AvailableTenants = userTenants.Select(ut => new TenantSelectionDto
                {
                    TenantId = ut.TenantId,
                    TenantName = ut.Tenant?.Name ?? "Unknown",
                    IsAdmin = ut.IsAdmin
                }).ToList(),
                Message = "Please select a tenant"
            };
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error handling default flow: {ex.Message}", ex);
        return new AuthCallbackResponseDto
        {
            Success = false,
            Flow = "default",
            RequiresTenantSelection = false,
            ErrorMessage = "An unexpected error occurred during authentication"
        };
    }
}
```

---

## 5. API Endpoint Updates

### 5.1 Add Endpoint to Configure SSO Settings

File: `GroundUp.api/Controllers/TenantController.cs`

```csharp
/// <summary>
/// Configure SSO auto-join settings for enterprise tenant
/// POST /api/tenants/{id}/sso-settings
/// </summary>
[HttpPost("{id}/sso-settings")]
public async Task<ActionResult<ApiResponse<TenantDto>>> ConfigureSsoSettings(
    int id,
    [FromBody] ConfigureSsoSettingsDto dto)
{
    try
    {
        var tenant = await _dbContext.Tenants.FindAsync(id);
        
        if (tenant == null)
        {
            return NotFound(new ApiResponse<TenantDto>(
                default!,
                false,
                "Tenant not found",
                null,
                StatusCodes.Status404NotFound,
                ErrorCodes.NotFound
            ));
        }
        
        if (tenant.TenantType != TenantType.Enterprise)
        {
            return BadRequest(new ApiResponse<TenantDto>(
                default!,
                false,
                "SSO settings only available for enterprise tenants",
                null,
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationFailed
            ));
        }
        
        // Update SSO settings
        tenant.SsoAutoJoinDomains = dto.SsoAutoJoinDomains;
        tenant.SsoAutoJoinRoleId = dto.SsoAutoJoinRoleId;
        
        if (dto.AuthProviderPolicy.HasValue)
        {
            tenant.AuthProviderPolicy = dto.AuthProviderPolicy.Value;
        }
        
        await _dbContext.SaveChangesAsync();
        
        var tenantDto = _mapper.Map<TenantDto>(tenant);
        
        return Ok(new ApiResponse<TenantDto>(
            tenantDto,
            true,
            "SSO settings updated successfully"
        ));
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error configuring SSO settings: {ex.Message}", ex);
        return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<TenantDto>(
            default!,
            false,
            "Failed to update SSO settings",
            new List<string> { ex.Message },
            StatusCodes.Status500InternalServerError,
            ErrorCodes.InternalServerError
        ));
    }
}
```

### 5.2 Create DTO

File: `GroundUp.Core/dtos/TenantDto.cs` (add new DTO):

```csharp
public class ConfigureSsoSettingsDto
{
    /// <summary>
    /// List of email domains allowed for auto-join
    /// Example: ["acme.com", "acmecorp.com"]
    /// Set to null or empty array to disable auto-join
    /// </summary>
    public List<string>? SsoAutoJoinDomains { get; set; }
    
    /// <summary>
    /// Default role ID to assign when users auto-join
    /// If null, uses tenant's default member role
    /// </summary>
    public int? SsoAutoJoinRoleId { get; set; }
    
    /// <summary>
    /// Authentication provider policy
    /// If null, keeps existing policy
    /// </summary>
    public AuthProviderPolicy? AuthProviderPolicy { get; set; }
}
```

---

## 6. Testing Guide

### 6.1 Manual Test: Invitation-Only Mode (Default)

```bash
# 1. Create enterprise tenant (already exists)
# Tenant ID: 6, Realm: tenant_rob01_8765

# 2. Verify SSO settings (should be null by default)
GET http://localhost:5123/api/tenants/6
Authorization: Bearer <admin-token>

# Expected: SsoAutoJoinDomains = null

# 3. Create invitation
POST http://localhost:5123/api/invitations
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "email": "ssouser@example.com",
  "isAdmin": false,
  "expirationDays": 7
}

# 4. Simulate SSO login WITHOUT invitation
# (User tries to login via Azure AD but wasn't invited)
# Expected: Access denied, Keycloak account deleted

# 5. Simulate SSO login WITH invitation
# Expected: Auto-accepts invitation, assigns to tenant
```

### 6.2 Manual Test: Auto-Join Mode

```bash
# 1. Configure auto-join for domain
POST http://localhost:5123/api/tenants/6/sso-settings
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "ssoAutoJoinDomains": ["acme.com", "acmecorp.com"],
  "ssoAutoJoinRoleId": 2,  # Member role ID
  "authProviderPolicy": 0   # EnterpriseOnly
}

# 2. Simulate SSO login with allowed domain
# User: employee@acme.com
# Expected: Auto-joins tenant, assigned Member role

# 3. Simulate SSO login with disallowed domain
# User: contractor@external.com
# Expected: Access denied (no invitation), Keycloak account deleted

# 4. Create invitation for contractor
POST http://localhost:5123/api/invitations
{
  "email": "contractor@external.com",
  "isAdmin": false,
  "expirationDays": 7
}

# 5. Simulate SSO login again
# Expected: Auto-accepts invitation, assigns to tenant
```

### 6.3 Test Auth Provider Policy

```bash
# 1. Set to EnterprisePlusSocial
POST http://localhost:5123/api/tenants/6/sso-settings
{
  "authProviderPolicy": 1
}

# 2. Simulate social auth login (Google personal)
# Expected: Allowed if email is verified

# 3. Set to EnterpriseOnly
POST http://localhost:5123/api/tenants/6/sso-settings
{
  "authProviderPolicy": 0
}

# 4. Simulate social auth login
# Expected: Rejected (unverified email from social provider)
```

---

## 7. Implementation Checklist

- [ ] Run migration to add columns to Tenants table
- [ ] Add `AuthProviderPolicy` enum to `TenantEnums.cs`
- [ ] Update `Tenant` entity with new properties
- [ ] Update `ApplicationDbContext` with relationship configuration
- [ ] Update DTOs (`TenantDto`, `CreateTenantDto`, `UpdateTenantDto`)
- [ ] Create `ConfigureSsoSettingsDto`
- [ ] Update AutoMapper profile
- [ ] Add `ValidateAndAssignSsoUserAsync` helper method to `AuthController`
- [ ] Update `HandleDefaultFlowAsync` to use new validation
- [ ] Add `ConfigureSsoSettings` endpoint to `TenantController`
- [ ] Test invitation-only mode (no auto-join)
- [ ] Test auto-join with allowed domains
- [ ] Test auto-join with disallowed domains (should require invitation)
- [ ] Test auth provider policies (EnterpriseOnly vs EnterprisePlusSocial)
- [ ] Document SSO configuration in admin guide

---

## 8. Key Design Decisions

### 8.1 Why Delete Keycloak Users?

**Problem**: Federated SSO creates Keycloak accounts automatically on first login.

**Solution**: Delete unauthorized accounts to:
- Prevent user enumeration attacks
- Keep Keycloak clean (no orphaned accounts)
- Allow retry flow (if user later gets invited, they can try again)

### 8.2 Why JSON Column for Domains?

**Alternative**: Separate `TenantAllowedDomain` table

**Chosen**: JSON column because:
- Simple queries (most operations just read the list)
- Typically 1-3 domains per tenant (not thousands)
- Easier admin UI (edit array vs manage rows)
- Can migrate to separate table later if needed

### 8.3 Why Enum for Auth Policy?

**Flexibility**: Downstream apps can choose security level:
- `EnterpriseOnly` ? Maximum security (SSO only)
- `EnterprisePlusSocial` ? Mixed workforce (verified emails)
- `Unrestricted` ? Development/testing only

---

## 9. Security Considerations

### 9.1 Email Verification

Enterprise SSO providers (Azure AD, Okta) **always** provide verified emails.

Social providers (Google, Facebook) **may not** verify emails or may let users hide them.

**Mitigation**: `AuthProviderPolicy` controls which providers are allowed.

### 9.2 Domain Spoofing

**Attack**: Attacker registers `admin@acme.com` on Gmail and tries to join.

**Mitigation**: 
- Enterprise SSO verifies domain ownership via SAML/OIDC federation
- Social providers can't spoof corporate domains (Gmail can't issue `@acme.com` addresses)
- If tenant uses `EnterprisePlusSocial`, only verified emails allowed

### 9.3 Orphaned Keycloak Accounts

**Problem**: Unauthorized user creates Keycloak account, gets rejected, account remains.

**Mitigation**: Delete Keycloak user on authorization failure.

---

## 10. Future Enhancements

### 10.1 Admin Approval Queue

Instead of auto-rejecting, put unauthorized users in approval queue:

```csharp
// Create PendingAccessRequest record
await _dbContext.PendingAccessRequests.AddAsync(new PendingAccessRequest
{
    UserId = userId,
    TenantId = tenant.Id,
    Email = userEmail,
    RequestedAt = DateTime.UtcNow,
    Status = AccessRequestStatus.Pending
});

// Notify tenant admins
// Show in admin dashboard
```

### 10.2 Role Mapping Based on IdP Claims

Map Azure AD groups to GroundUp roles:

```csharp
var groups = jwtToken.Claims
    .Where(c => c.Type == "groups")
    .Select(c => c.Value)
    .ToList();

if (groups.Contains("Engineering"))
{
    // Assign "Developer" role
}
else if (groups.Contains("Management"))
{
    // Assign "Manager" role
}
```

### 10.3 Just-In-Time Provisioning

Pre-create Keycloak users when invitation is sent (for local accounts).

Already implemented in spec section 2.4.1 (Local Account Invitations).

---

## 11. References

- **Spec**: `docs/groundup-auth-spec-for-copilot.md` (sections 2.4.1 and 2.4.2)
- **Original Discussion**: `docs/INVITATION-URL-BUG-CURRENT-STATE.md`
- **Related**: `docs/ENTERPRISE-FIRST-ADMIN-FLOW.md`

---

**Status**: Ready for implementation  
**Priority**: High (blocks enterprise SSO invitations)  
**Estimated Effort**: 4-6 hours  
**Testing Required**: Manual API testing with Postman/Swagger

---

**END OF IMPLEMENTATION GUIDE**
