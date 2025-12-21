# Enterprise SSO Invitation Implementation Guide (Simplified)

## Overview

This document provides implementation guidance for enterprise SSO invitation flows with domain-based auto-join capabilities. This is a **simplified version** that removes redundant validations and trusts Keycloak's IdP configuration.

---

## Key Design Principles

### ? **What We Validate**
1. **Email presence** - Required for invitation/domain matching (business logic)
2. **Domain allowlist** - Auto-join authorized domains
3. **Invitation existence** - Only invited users can access

### ? **What We Don't Validate**
1. ~~Email verification~~ - Keycloak handles this via realm settings
2. ~~Auth provider policy~~ - Keycloak IdP configuration IS the policy
3. ~~Social vs Enterprise IdP~~ - Admin chooses which IdPs to enable in Keycloak

### ?? **Separation of Concerns**
- **Keycloak**: Manages which IdPs are enabled, email verification enforcement
- **Application**: Enforces business rules (invitations, domain allowlists, tenant assignment)

---

## 1. Database Schema Changes

### 1.1 Add Columns to `Tenants` Table

```sql
-- Add SSO auto-join configuration
ALTER TABLE Tenants
ADD SsoAutoJoinDomains NVARCHAR(MAX) NULL,  -- JSON array of allowed domains
    SsoAutoJoinRoleId INT NULL;             -- Default role for auto-join users

-- Add foreign key constraint
ALTER TABLE Tenants
ADD CONSTRAINT FK_Tenants_SsoAutoJoinRoleId 
    FOREIGN KEY (SsoAutoJoinRoleId) REFERENCES Roles(Id)
    ON DELETE SET NULL;
```

**Note:** No `AuthProviderPolicy` or `RequireVerifiedEmail` columns needed!

---

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

        migrationBuilder.CreateIndex(
            name: "IX_Tenants_SsoAutoJoinRoleId",
            table: "Tenants",
            column: "SsoAutoJoinRoleId");

        migrationBuilder.AddForeignKey(
            name: "FK_Tenants_Roles_SsoAutoJoinRoleId",
            table: "Tenants",
            column: "SsoAutoJoinRoleId",
            principalTable: "Roles",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
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
    }
}
```

---

## 2. Core Entity Updates

### 2.1 Update `Tenant` Entity

File: `GroundUp.core/entities/Tenant.cs`

Add these properties to the existing `Tenant` class:

```csharp
using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;

public class Tenant
{
    // ...existing properties...
    
    /// <summary>
    /// JSON array of email domains allowed for SSO auto-join
    /// Example JSON: ["acme.com", "acmecorp.com"]
    /// If null/empty: All users require explicit invitation
    /// If populated: Users from these domains can auto-join on first SSO login
    /// </summary>
    public string? SsoAutoJoinDomainsJson { get; set; }
    
    /// <summary>
    /// Parsed list of allowed domains (not mapped to database)
    /// Use this property in application code
    /// </summary>
    [NotMapped]
    public List<string>? SsoAutoJoinDomains
    {
        get => string.IsNullOrEmpty(SsoAutoJoinDomainsJson)
            ? null
            : JsonSerializer.Deserialize<List<string>>(SsoAutoJoinDomainsJson);
        set => SsoAutoJoinDomainsJson = value == null || value.Count == 0
            ? null
            : JsonSerializer.Serialize(value);
    }
    
    /// <summary>
    /// Default role ID assigned when users auto-join via allowed domain
    /// If null: Uses tenant's default member role (e.g., "Member")
    /// </summary>
    public int? SsoAutoJoinRoleId { get; set; }
    
    /// <summary>
    /// Navigation property for auto-join role
    /// </summary>
    public Role? SsoAutoJoinRole { get; set; }
}
```

### 2.2 Update `ApplicationDbContext`

File: `GroundUp.infrastructure/data/ApplicationDbContext.cs`

Add to `OnModelCreating` method:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ...existing configurations...
    
    // Tenant SSO auto-join configuration
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

File: `GroundUp.core/dtos/TenantDto.cs`

Add these properties:

```csharp
public class TenantDto
{
    // ...existing properties...
    
    /// <summary>
    /// List of email domains allowed for auto-join via SSO
    /// </summary>
    public List<string>? SsoAutoJoinDomains { get; set; }
    
    /// <summary>
    /// Default role ID for auto-joined users
    /// </summary>
    public int? SsoAutoJoinRoleId { get; set; }
    
    /// <summary>
    /// Name of the auto-join role (for display purposes)
    /// </summary>
    public string? SsoAutoJoinRoleName { get; set; }
}
```

### 3.2 Update `CreateTenantDto` and `UpdateTenantDto`

Add to both DTOs:

```csharp
public List<string>? SsoAutoJoinDomains { get; set; }
public int? SsoAutoJoinRoleId { get; set; }
```

### 3.3 Create `ConfigureSsoSettingsDto`

File: `GroundUp.core/dtos/TenantDto.cs` (add to existing file)

```csharp
/// <summary>
/// DTO for configuring SSO auto-join settings
/// </summary>
public class ConfigureSsoSettingsDto
{
    /// <summary>
    /// List of email domains allowed for auto-join
    /// Example: ["acme.com", "acmecorp.com"]
    /// Set to null or empty array to disable auto-join (invitation-only mode)
    /// </summary>
    public List<string>? SsoAutoJoinDomains { get; set; }
    
    /// <summary>
    /// Default role ID to assign when users auto-join
    /// If null, uses tenant's default member role
    /// </summary>
    public int? SsoAutoJoinRoleId { get; set; }
}
```

### 3.4 Update AutoMapper Profile

File: `GroundUp.infrastructure/mappings/MappingProfile.cs`

Update tenant mappings:

```csharp
// In constructor
CreateMap<Tenant, TenantDto>()
    .ForMember(dest => dest.SsoAutoJoinDomains, 
        opt => opt.MapFrom(src => src.SsoAutoJoinDomains))
    .ForMember(dest => dest.SsoAutoJoinRoleName, 
        opt => opt.MapFrom(src => src.SsoAutoJoinRole != null ? src.SsoAutoJoinRole.Name : null));

CreateMap<CreateTenantDto, Tenant>()
    .ForMember(dest => dest.SsoAutoJoinDomains, 
        opt => opt.MapFrom(src => src.SsoAutoJoinDomains));

CreateMap<UpdateTenantDto, Tenant>()
    .ForMember(dest => dest.SsoAutoJoinDomains, 
        opt => opt.MapFrom(src => src.SsoAutoJoinDomains));
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
    string? userEmail,
    Tenant tenant,
    string realm)
{
    _logger.LogInformation($"Validating SSO user {userEmail ?? "no-email"} for enterprise tenant {tenant.Name}");
    
    // 1. For ENTERPRISE tenants, email is REQUIRED
    //    (Needed for invitation matching and domain-based auto-join)
    if (tenant.TenantType == TenantType.Enterprise && string.IsNullOrEmpty(userEmail))
    {
        _logger.LogWarning($"Enterprise login without email for realm {realm}");
        return (false, "Your authentication provider did not share your email address. Enterprise access requires a verified email.");
    }
    
    // 2. Email validation passed - continue with authorization checks
    
    // 3. Check domain-based auto-join
    var userDomain = userEmail!.Split('@')[1].ToLowerInvariant();
    
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
            
            if (roleId == null)
            {
                _logger.LogWarning($"No default role found for tenant {tenant.Id}");
            }
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
    
    // 4. Check for pending invitation
    var pendingInvitations = await _tenantInvitationRepository
        .GetInvitationsForEmailAsync(userEmail!);
    
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
    
    // 5. No authorization found - user is not allowed
    _logger.LogWarning($"Unauthorized SSO login attempt for {userEmail} in tenant {tenant.Name}");
    return (false, "Access denied. Please request an invitation from your administrator.");
}
```

### 4.2 Update `HandleDefaultFlowAsync`

Modify the existing method in `AuthController.cs`:

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
                ErrorMessage = "User not found in authentication system"
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

## 5. API Endpoint: Configure SSO Settings

### 5.1 Add Endpoint to `TenantController`

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
        
        await _dbContext.SaveChangesAsync();
        
        var tenantDto = _mapper.Map<TenantDto>(tenant);
        
        _logger.LogInformation($"Updated SSO settings for tenant {id}: Domains={string.Join(",", dto.SsoAutoJoinDomains ?? new List<string>())}, RoleId={dto.SsoAutoJoinRoleId}");
        
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

---

## 6. Testing Guide

### 6.1 Test: Invitation-Only Mode (Default)

**Setup:**
- Enterprise tenant with no auto-join domains configured

**Steps:**
```bash
# 1. Verify SSO settings (should be null)
GET http://localhost:5123/api/tenants/6
Authorization: Bearer <admin-token>

# Expected: SsoAutoJoinDomains = null

# 2. Create invitation
POST http://localhost:5123/api/invitations
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "email": "invited@example.com",
  "isAdmin": false,
  "expirationDays": 7
}

# 3. Try SSO login WITHOUT invitation
# Expected: "Access denied. Please request an invitation."
# Keycloak account deleted

# 4. Try SSO login WITH invitation
# Expected: Auto-accepts invitation, assigns to tenant, user logged in
```

---

### 6.2 Test: Auto-Join Mode

**Setup:**
- Configure auto-join domains

**Steps:**
```bash
# 1. Configure auto-join for domain
POST http://localhost:5123/api/tenants/6/sso-settings
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "ssoAutoJoinDomains": ["acme.com", "acmecorp.com"],
  "ssoAutoJoinRoleId": 2  # Member role ID
}

# 2. Try SSO login with ALLOWED domain
# User: employee@acme.com
# Expected: Auto-joins tenant, assigned Member role

# 3. Try SSO login with DISALLOWED domain
# User: contractor@external.com
# Expected: "Access denied" (no invitation)
# Keycloak account deleted

# 4. Create invitation for contractor
POST http://localhost:5123/api/invitations
{
  "email": "contractor@external.com",
  "isAdmin": false,
  "expirationDays": 7
}

# 5. Try SSO login again
# Expected: Auto-accepts invitation, assigns to tenant
```

---

### 6.3 Test: Email Required for Enterprise

**Steps:**
```bash
# 1. Configure Keycloak to allow social auth (e.g., Facebook)
# 2. User attempts login via Facebook (email declined)
# Expected: "Enterprise access requires a verified email."
# Keycloak account deleted

# 3. User attempts login via Azure AD (enterprise SSO)
# Expected: Email always present, login succeeds (if invited/allowed)
```

---

## 7. Implementation Checklist

- [ ] Run migration to add columns to Tenants table
- [ ] Update `Tenant` entity with SSO properties
- [ ] Update `ApplicationDbContext` with relationship configuration
- [ ] Update DTOs (`TenantDto`, `CreateTenantDto`, `UpdateTenantDto`)
- [ ] Create `ConfigureSsoSettingsDto`
- [ ] Update AutoMapper profile
- [ ] Add `ValidateAndAssignSsoUserAsync` method to `AuthController`
- [ ] Update `HandleDefaultFlowAsync` to use new validation
- [ ] Add `ConfigureSsoSettings` endpoint to `TenantController`
- [ ] Test invitation-only mode (no auto-join)
- [ ] Test auto-join with allowed domains
- [ ] Test auto-join with disallowed domains (requires invitation)
- [ ] Test email requirement enforcement for enterprise tenants
- [ ] Document SSO configuration in admin guide

---

## 8. Key Design Decisions

### 8.1 Email Validation (Required)

**Why:** Email is required for:
- Invitation matching by email address
- Domain extraction for auto-join logic
- User record creation (User.Email is non-nullable)

**Enforcement:** Application validates email presence for enterprise tenants.

---

### 8.2 No Email Verification Check

**Why:** Keycloak already handles this via:
- Realm settings (`verifyEmail: true`)
- Required actions (`VERIFY_EMAIL`)
- IdP configuration (enterprise SSO always verified)

**Result:** Application trusts Keycloak's verification. No redundant checks.

---

### 8.3 No Auth Provider Policy Enum

**Why:** Keycloak IdP configuration IS the policy:
- Admin enables Azure AD ? Users can use Azure AD
- Admin enables Google ? Users can use Google
- Admin enables both ? Both allowed

**Result:** Application respects Keycloak's configuration. No duplication.

---

### 8.4 JSON Column for Domains

**Why:** Simple and efficient:
- Typically 1-3 domains per tenant
- Easy to query and update
- Simple admin UI (edit array)

**Alternative:** Could use separate `TenantAllowedDomain` table if needed later.

---

### 8.5 Delete Unauthorized Keycloak Users

**Why:**
- Prevents user enumeration attacks
- Keeps Keycloak clean (no orphaned accounts)
- Allows retry flow (user can try again after getting invited)

---

## 9. Security Considerations

### 9.1 Email as Identity

Enterprise invitations **require** email for matching. Users without email cannot use invitation-based access (by design).

### 9.2 Domain Verification

Enterprise SSO providers (Azure AD, Okta) verify domain ownership via federation. Social providers cannot spoof corporate domains.

### 9.3 Keycloak Account Cleanup

Unauthorized SSO attempts result in Keycloak account deletion, preventing orphaned accounts and enumeration.

---

## 10. Future Enhancements

### 10.1 Admin Approval Queue

Instead of auto-rejecting, queue unauthorized users for admin approval:

```csharp
await _dbContext.PendingAccessRequests.AddAsync(new PendingAccessRequest
{
    UserId = userId,
    TenantId = tenant.Id,
    Email = userEmail,
    RequestedAt = DateTime.UtcNow,
    Status = AccessRequestStatus.Pending
});
```

### 10.2 IdP Group Mapping

Map Azure AD/Okta groups to GroundUp roles:

```csharp
var groups = jwtToken.Claims
    .Where(c => c.Type == "groups")
    .Select(c => c.Value);

if (groups.Contains("Engineering"))
{
    // Assign "Developer" role
}
```

### 10.3 Standard Tenant Email Optionality

For non-enterprise apps (gaming, social), consider making email optional and using username as fallback identifier.

---

## 11. References

- **Spec**: `docs/groundup-auth-spec-for-copilot.md` (sections 2.4)
- **Original Discussion**: `docs/INVITATION-URL-BUG-CURRENT-STATE.md`
- **Enterprise First Admin**: `docs/ENTERPRISE-FIRST-ADMIN-FLOW.md`

---

**Status**: Ready for implementation  
**Priority**: High (blocks enterprise SSO invitations)  
**Estimated Effort**: 3-4 hours  
**Testing Required**: Manual API testing with Postman/Swagger

---

**END OF SIMPLIFIED IMPLEMENTATION GUIDE**
