# GroundUp - Continue Multi-Tenant Invitation System Implementation

## Context

This document contains the full context needed to continue implementing the invitation-based multi-tenant user assignment system in GroundUp.

---

## What We're Building

**Goal:** Replace the flawed auto-tenant-assignment approach with an invitation-based system where:
1. Admins create invitations for users to join specific tenants
2. Users authenticate via Keycloak (any method: password, Google, Azure AD, etc.)
3. Users accept invitations to be assigned to tenants
4. Supports multi-tenant users (one user can belong to multiple tenants)

---

## Progress So Far

### ? Completed Steps (Step 1a-1e)

**Step 1a: Removed User.TenantId**
- Removed `TenantId` property from `User` entity
- Removed `ITenantEntity` interface from `User`
- Users now belong to tenants ONLY via `UserTenants` junction table
- File: `GroundUp.core/entities/User.cs`

**Step 1b: Added UserTenant.IsAdmin**
- Added `IsAdmin` field to `UserTenant` entity
- Added `JoinedAt` timestamp
- Added `User` navigation property
- Tracks per-tenant admin privileges (user can be admin of Tenant A, regular user in Tenant B)
- File: `GroundUp.core/entities/UserTenant.cs`

**Step 1c: Added Tenant.ParentTenantId**
- Added `ParentTenantId` (nullable) for hierarchical tenants
- Added `CreatedAt` and `IsActive` fields
- Added `ParentTenant` and `ChildTenants` navigation properties
- Supports parent/child tenant relationships
- File: `GroundUp.core/entities/Tenant.cs`

**Step 1d: Created TenantInvitation Entity**
- Created complete `TenantInvitation` entity with:
  - Email, TenantId, InvitationToken (GUID)
  - ExpiresAt, IsAccepted, AcceptedAt, AcceptedByUserId
  - CreatedByUserId, CreatedAt
  - AssignedRole (optional Keycloak role)
  - IsAdmin (make user tenant admin when accepted)
  - Metadata (JSON for extensibility)
  - Navigation properties: Tenant, AcceptedByUser, CreatedByUser
  - Computed properties: IsExpired, IsValid
- File: `GroundUp.core/entities/TenantInvitation.cs`

**Step 1e: Database Migration ? COMPLETED**
- ? Added `TenantInvitations` DbSet to `ApplicationDbContext`
- ? Configured EF Core relationships for `TenantInvitation`:
  - Unique index on `InvitationToken`
  - Index on `Email`
  - Foreign keys to `Tenant`, `AcceptedByUser`, and `CreatedByUser`
  - Proper delete behaviors (Cascade for Tenant, Restrict for Users)
  - DATETIME(6) column types for timestamps
- ? Configured `Tenant` hierarchical relationship (self-referencing)
- ? Configured `UserTenant` relationships and timestamps
- ? Created migration: `20251120034229_AddInvitationBasedTenantAssignment`
- ? Applied migration to database successfully
- File: `GroundUp.infrastructure/data/ApplicationDbContext.cs`

**Step 1f: Updated UserTenant Repository ? COMPLETED**
- ? Updated `IUserTenantRepository.AssignUserToTenantAsync` to include `isAdmin` parameter
- ? Updated `UserTenantRepository.AssignUserToTenantAsync` implementation to:
  - Set `IsAdmin` flag when assigning users
  - Set `JoinedAt` timestamp automatically
  - Update `IsAdmin` if user already exists in tenant
- ? Updated `UserTenantDto` to include `IsAdmin` and `JoinedAt` fields
- Files: 
  - `GroundUp.core/interfaces/IUserTenantRepository.cs`
  - `GroundUp.infrastructure/repositories/UserTenantRepository.cs`
  - `GroundUp.core/dtos/UserTenantDto.cs`

### ?? Current Step: Step 2 - Create Invitation DTOs

**What needs to be done:**
1. Create DTOs in `GroundUp.core/dtos/TenantInvitationDtos.cs`
2. Add AutoMapper configuration for TenantInvitation

---

## Current Entity State

### User.cs (MODIFIED)
```csharp
public class User
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    // REMOVED: public int TenantId { get; set; }
    // Users belong to tenants via UserTenants table only
    
    public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
```

### UserTenant.cs (MODIFIED)
```csharp
public class UserTenant
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public int TenantId { get; set; }
    public bool IsAdmin { get; set; } = false; // NEW
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow; // NEW
    
    public User? User { get; set; } // NEW
    public Tenant? Tenant { get; set; }
}
```

### Tenant.cs (MODIFIED)
```csharp
public class Tenant
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? ParentTenantId { get; set; } // NEW
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // NEW
    public bool IsActive { get; set; } = true; // NEW
    
    public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
    public Tenant? ParentTenant { get; set; } // NEW
    public ICollection<Tenant> ChildTenants { get; set; } = new List<Tenant>(); // NEW
}
```

### TenantInvitation.cs (NEW)
```csharp
public class TenantInvitation
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public required string Email { get; set; }
    
    public int TenantId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public required string InvitationToken { get; set; }
    
    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; } = false;
    public DateTime? AcceptedAt { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(50)]
    public string? AssignedRole { get; set; }
    
    public bool IsAdmin { get; set; } = false;
    public string? Metadata { get; set; }
    
    // Navigation properties
    public Tenant? Tenant { get; set; }
    public User? AcceptedByUser { get; set; }
    public User? CreatedByUser { get; set; }
    
    // Computed properties
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsAccepted && !IsExpired;
}
```

---

## Next Steps (Remaining Work)

### Step 2: Create Invitation DTOs (IN PROGRESS)

**File to create:** `GroundUp.core/dtos/TenantInvitationDtos.cs`

**DTOs needed:**
```csharp
public class CreateTenantInvitationDto
{
    public required string Email { get; set; }
    public int TenantId { get; set; }
    public string? AssignedRole { get; set; }
    public bool IsAdmin { get; set; } = false;
    public int ExpirationDays { get; set; } = 7;
}

public class TenantInvitationDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string InvitationToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? AssignedRole { get; set; }
    public bool IsAdmin { get; set; }
}

public class AcceptInvitationDto
{
    public required string InvitationToken { get; set; }
}
```

**AutoMapper configuration to add in `MappingProfile.cs`:**
```csharp
// TenantInvitation mappings
CreateMap<TenantInvitation, TenantInvitationDto>()
    .ForMember(dest => dest.TenantName, opt => opt.MapFrom(src => src.Tenant != null ? src.Tenant.Name : string.Empty))
    .ForMember(dest => dest.CreatedByUserName, opt => opt.MapFrom(src => src.CreatedByUser != null ? src.CreatedByUser.Username : string.Empty))
    .ForMember(dest => dest.IsExpired, opt => opt.MapFrom(src => src.IsExpired));
```

---

### Step 3: Create Invitation Repository

**Files to create:**

**GroundUp.core/interfaces/ITenantInvitationRepository.cs:**
```csharp
public interface ITenantInvitationRepository
{
    Task<TenantInvitation> CreateInvitationAsync(CreateTenantInvitationDto dto, Guid createdByUserId);
    Task<TenantInvitation?> GetByTokenAsync(string token);
    Task<TenantInvitation?> GetByIdAsync(int id);
    Task<List<TenantInvitation>> GetInvitationsForTenantAsync(int tenantId);
    Task<List<TenantInvitation>> GetPendingInvitationsForTenantAsync(int tenantId);
    Task<bool> AcceptInvitationAsync(string token, Guid userId);
    Task<bool> RevokeInvitationAsync(int invitationId);
    Task<bool> ResendInvitationAsync(int invitationId);
    Task UpdateAsync(TenantInvitation invitation);
}
```

**GroundUp.infrastructure/repositories/TenantInvitationRepository.cs:**
```csharp
public class TenantInvitationRepository : ITenantInvitationRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IUserTenantRepository _userTenantRepo;
    private readonly IIdentityProviderAdminService _identityProvider;
    private readonly ILoggingService _logger;

    public async Task<TenantInvitation> CreateInvitationAsync(CreateTenantInvitationDto dto, Guid createdByUserId)
    {
        var invitation = new TenantInvitation
        {
            Email = dto.Email,
            TenantId = dto.TenantId,
            InvitationToken = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpirationDays),
            AssignedRole = dto.AssignedRole,
            IsAdmin = dto.IsAdmin,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        return invitation;
    }

    public async Task<bool> AcceptInvitationAsync(string token, Guid userId)
    {
        var invitation = await GetByTokenAsync(token);

        if (invitation == null || !invitation.IsValid)
            return false;

        // Assign user to tenant with IsAdmin flag
        await _userTenantRepo.AssignUserToTenantAsync(
            userId, 
            invitation.TenantId, 
            invitation.IsAdmin
        );

        // Assign role if specified
        if (!string.IsNullOrEmpty(invitation.AssignedRole))
        {
            await _identityProvider.AssignRoleToUserAsync(
                userId.ToString(), 
                invitation.AssignedRole
            );
        }

        // Mark invitation as accepted
        invitation.IsAccepted = true;
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUserId = userId;
        await _context.SaveChangesAsync();

        return true;
    }

    // ... other methods
}
```

---

### Step 4: Create Invitation Controller

**File:** `GroundUp.api/Controllers/TenantInvitationController.cs`

```csharp
[ApiController]
[Route("api")]
[Authorize]
public class TenantInvitationController : ControllerBase
{
    private readonly ITenantInvitationRepository _invitationRepo;

    // POST /api/tenants/{tenantId}/invitations
    [HttpPost("tenants/{tenantId}/invitations")]
    [RequiresPermission("tenant.manage_users")]
    public async Task<IActionResult> CreateInvitation(
        int tenantId,
        [FromBody] CreateTenantInvitationDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var invitation = await _invitationRepo.CreateInvitationAsync(dto, userId);
        
        // TODO: Send invitation email
        
        return Ok(new ApiResponse<TenantInvitationDto>(invitation));
    }

    // POST /api/invitations/accept
    [HttpPost("invitations/accept")]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var success = await _invitationRepo.AcceptInvitationAsync(dto.InvitationToken, userId);
        
        if (!success)
            return BadRequest(new ApiResponse<bool>(false, false, "Invalid or expired invitation"));
        
        return Ok(new ApiResponse<bool>(true, true, "Invitation accepted successfully"));
    }

    // GET /api/tenants/{tenantId}/invitations
    [HttpGet("tenants/{tenantId}/invitations")]
    [RequiresPermission("tenant.manage_users")]
    public async Task<IActionResult> GetInvitations(int tenantId)
    {
        var invitations = await _invitationRepo.GetInvitationsForTenantAsync(tenantId);
        return Ok(new ApiResponse<List<TenantInvitation>>(invitations));
    }
}
```

---

### Step 5: Auth Callback Handler (Future)

**File:** `GroundUp.api/Controllers/AuthController.cs` (or new controller)

```csharp
// GET /auth/callback
[HttpGet("auth/callback")]
[AllowAnonymous]
public async Task<IActionResult> AuthCallback(
    [FromQuery] string code,
    [FromQuery] string state)
{
    // 1. Exchange code for tokens with Keycloak
    // 2. Extract user ID from JWT
    // 3. Sync user to database
    // 4. Decode state to determine flow (new-tenant vs invitation)
    // 5. Handle accordingly
}
```

---

### Step 6: Remove POST /api/users (Future)

**File:** `GroundUp.api/Controllers/UserController.cs`

- Deprecate or remove the `POST /api/users` endpoint
- Users should only be created via Keycloak

---

## Important Files Reference

### Entities
- `GroundUp.core/entities/User.cs` - ? Modified (removed TenantId)
- `GroundUp.core/entities/UserTenant.cs` - ? Modified (added IsAdmin, JoinedAt)
- `GroundUp.core/entities/Tenant.cs` - ? Modified (added ParentTenantId, etc.)
- `GroundUp.core/entities/TenantInvitation.cs` - ? NEW (created)

### Data
- `GroundUp.infrastructure/data/ApplicationDbContext.cs` - ? UPDATED

### DTOs
- `GroundUp.core/dtos/UserTenantDto.cs` - ? UPDATED (added IsAdmin, JoinedAt)
- `GroundUp.core/dtos/TenantInvitationDtos.cs` - ?? TO CREATE

### Repositories
- `GroundUp.infrastructure/repositories/UserRepository.cs` - ? Modified (removed TenantId refs)
- `GroundUp.infrastructure/repositories/UserTenantRepository.cs` - ? UPDATED (IsAdmin support)
- `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs` - TO CREATE

### Interfaces
- `GroundUp.core/interfaces/IUserTenantRepository.cs` - ? UPDATED (IsAdmin parameter)
- `GroundUp.core/interfaces/ITenantInvitationRepository.cs` - TO CREATE

---

## Key Decisions Made

1. **? Keycloak is ONLY place users are created** - App only assigns tenants
2. **? Invitation-based tenant assignment** - No auto-assignment
3. **? Multi-tenant users supported** - Users can belong to multiple tenants
4. **? Per-tenant admin privileges** - UserTenant.IsAdmin field
5. **? Hierarchical tenants supported** - Tenant.ParentTenantId field
6. **? Extensible invitations** - Metadata JSON field for future customization
7. **? Email always required** - All auth providers give email

---

## Architecture Principles

### User Creation Flow
```
User ? Keycloak (any auth method) ? App syncs to DB ? Invitation assigns tenant
```

### Invitation Flow
```
Admin creates invitation
    ?
User receives email with token
    ?
User clicks link (may not be logged in)
    ?
Redirects to Keycloak for auth
    ?
User authenticates (any method: password, Google, Azure AD)
    ?
Redirects back to app with token
    ?
User accepts invitation
    ?
App assigns user to tenant based on invitation
```

---

## Testing Checklist

After implementation, test:

- [ ] Create invitation via API
- [ ] Verify invitation stored in DB
- [ ] Verify unique token generated
- [ ] Accept invitation as authenticated user
- [ ] Verify user assigned to correct tenant
- [ ] Verify IsAdmin flag set correctly
- [ ] Verify AssignedRole applied
- [ ] Verify invitation marked as accepted
- [ ] Test expired invitation (should fail)
- [ ] Test already-accepted invitation (should fail)
- [ ] Test hierarchical tenants (parent/child)
- [ ] Test multi-tenant user (accept multiple invitations)

---

## Build Verification

Before proceeding, verify:
- [ ] `dotnet build` succeeds
- [ ] No compilation errors
- [ ] All entity changes compile correctly

---

## Current Build Status

? **Last build: SUCCESSFUL**

All entity changes (User, UserTenant, Tenant, TenantInvitation) compile without errors.
Database migration created and applied successfully.
UserTenant repository updated with IsAdmin support.

**Migration Applied:** `20251120034229_AddInvitationBasedTenantAssignment`

**Database Changes:**
- ? Removed `Users.TenantId` column
- ? Added `UserTenants.IsAdmin` column
- ? Added `UserTenants.JoinedAt` column
- ? Added `Tenants.ParentTenantId` column
- ? Added `Tenants.CreatedAt` column
- ? Added `Tenants.IsActive` column
- ? Created `TenantInvitations` table with all columns and indexes
- ? Created all foreign key relationships
- ? Created unique index on `TenantInvitations.InvitationToken`
- ? Created index on `TenantInvitations.Email`
