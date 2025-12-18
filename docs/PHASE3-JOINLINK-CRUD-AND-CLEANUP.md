# Phase 3: Join-Link CRUD APIs & UserKeycloakIdentity Cleanup

## Context from Previous Threads

### ? Completed:
- **Phase 1**: Auth callback & public invitation endpoints (`/api/invitations/invite/{token}`, `/api/invitations/enterprise/invite/{token}`)
- **Phase 2**: UserKeycloakIdentity removed from AuthController, Join-link endpoint implemented (`/api/join/{token}`)
- **Build Status**: All changes compile successfully
- **Architecture**: `UserTenant.ExternalUserId` is single source of truth for identity ? tenant mapping

### ?? Current State:
- AuthController handles all flows: invitation, join_link, new_org, default
- Public endpoints redirect to Keycloak with OIDC state
- Auth callback populates `UserTenant.ExternalUserId` during tenant assignment
- Enterprise signup creates realm + tenant + invitation (email sending TODO)
- **UserKeycloakIdentity files still exist but are NOT used in AuthController**

### ?? Files Status:
- `UserKeycloakIdentityRepository.cs` has duplicate/malformed code (needs cleanup or removal)
- `ApplicationDbContext` still has `DbSet<UserKeycloakIdentity>` and `DbSet<AccountLinkToken>`
- These entities/repositories are not registered in DI and not used anywhere

---

## Task: Join-Link CRUD APIs & UserKeycloakIdentity Cleanup

### Priority 1: Join-Link Management APIs (High Priority)

**Goal**: Allow tenant admins to create, list, and revoke join links for open registration

**Endpoints to Create**:

#### 1. Create Join Link
```
POST /api/tenant-join-links
Authorization: Bearer {tenant-scoped-token}
Content-Type: application/json

{
  "expirationDays": 30,
  "defaultRoleId": null  // optional, for future role assignment
}

Response 201:
{
  "success": true,
  "data": {
    "id": 1,
    "joinToken": "abc123def456...",
    "joinUrl": "https://api.example.com/api/join/abc123def456...",
    "expiresAt": "2024-02-15T12:00:00Z",
    "isRevoked": false,
    "createdAt": "2024-01-15T12:00:00Z"
  }
}
```

#### 2. List Join Links
```
GET /api/tenant-join-links?includeRevoked=false
Authorization: Bearer {tenant-scoped-token}

Response 200:
{
  "success": true,
  "data": [
    {
      "id": 1,
      "joinToken": "abc123...",
      "expiresAt": "2024-02-15T12:00:00Z",
      "isRevoked": false,
      "createdAt": "2024-01-15T12:00:00Z"
    }
  ]
}
```

#### 3. Revoke Join Link
```
DELETE /api/tenant-join-links/{id}
Authorization: Bearer {tenant-scoped-token}

Response 200:
{
  "success": true,
  "message": "Join link revoked successfully"
}
```

---

### Implementation Steps

#### Step 1: Create DTOs

**File**: `GroundUp.core/dtos/TenantJoinLinkDtos.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.dtos
{
    /// <summary>
    /// DTO for creating a new join link
    /// TenantId is automatically determined from ITenantContext
    /// </summary>
    public class CreateTenantJoinLinkDto
    {
        [Range(1, 365)]
        public int ExpirationDays { get; set; } = 30;
        
        /// <summary>
        /// Optional default role to assign when users join via this link
        /// </summary>
        public int? DefaultRoleId { get; set; }
    }

    /// <summary>
    /// DTO for join link details (response)
    /// </summary>
    public class TenantJoinLinkDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string JoinToken { get; set; } = string.Empty;
        public string JoinUrl { get; set; } = string.Empty;
        public bool IsRevoked { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? DefaultRoleId { get; set; }
    }
}
```

#### Step 2: Create Repository Interface

**File**: `GroundUp.core/interfaces/ITenantJoinLinkRepository.cs`

```csharp
using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface ITenantJoinLinkRepository
    {
        // Tenant-scoped operations (use ITenantContext)
        Task<ApiResponse<PaginatedData<TenantJoinLinkDto>>> GetAllAsync(FilterParams filterParams, bool includeRevoked = false);
        Task<ApiResponse<TenantJoinLinkDto>> GetByIdAsync(int id);
        Task<ApiResponse<TenantJoinLinkDto>> CreateAsync(CreateTenantJoinLinkDto dto);
        Task<ApiResponse<bool>> RevokeAsync(int id);
        
        // Cross-tenant operation (for public join endpoint)
        Task<ApiResponse<TenantJoinLinkDto>> GetByTokenAsync(string token);
    }
}
```

#### Step 3: Implement Repository

**File**: `GroundUp.infrastructure/repositories/TenantJoinLinkRepository.cs`

```csharp
using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class TenantJoinLinkRepository : BaseTenantRepository<TenantJoinLink, TenantJoinLinkDto>, ITenantJoinLinkRepository
    {
        public TenantJoinLinkRepository(
            ApplicationDbContext context,
            IMapper mapper,
            ILoggingService logger,
            ITenantContext tenantContext)
            : base(context, mapper, logger, tenantContext) { }

        public async Task<ApiResponse<PaginatedData<TenantJoinLinkDto>>> GetAllAsync(FilterParams filterParams, bool includeRevoked = false)
        {
            try
            {
                var tenantId = _tenantContext.TenantId;
                
                var query = _dbSet
                    .Where(j => j.TenantId == tenantId);
                
                if (!includeRevoked)
                {
                    query = query.Where(j => !j.IsRevoked);
                }
                
                query = query.OrderByDescending(j => j.CreatedAt);
                
                var totalRecords = await query.CountAsync();
                var items = await query
                    .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                    .Take(filterParams.PageSize)
                    .ToListAsync();
                
                var dtos = _mapper.Map<List<TenantJoinLinkDto>>(items);
                var paginatedData = new PaginatedData<TenantJoinLinkDto>(dtos, filterParams.PageNumber, filterParams.PageSize, totalRecords);
                
                return new ApiResponse<PaginatedData<TenantJoinLinkDto>>(paginatedData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting join links: {ex.Message}", ex);
                return new ApiResponse<PaginatedData<TenantJoinLinkDto>>(
                    default!,
                    false,
                    "Failed to retrieve join links",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public override async Task<ApiResponse<TenantJoinLinkDto>> GetByIdAsync(int id)
        {
            try
            {
                var joinLink = await _dbSet.FirstOrDefaultAsync(j => j.Id == id);
                if (joinLink == null)
                {
                    return new ApiResponse<TenantJoinLinkDto>(
                        default!,
                        false,
                        "Join link not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }
                
                var dto = _mapper.Map<TenantJoinLinkDto>(joinLink);
                return new ApiResponse<TenantJoinLinkDto>(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting join link: {ex.Message}", ex);
                return new ApiResponse<TenantJoinLinkDto>(
                    default!,
                    false,
                    "Failed to retrieve join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<TenantJoinLinkDto>> CreateAsync(CreateTenantJoinLinkDto dto)
        {
            try
            {
                var tenantId = _tenantContext.TenantId;
                
                var joinLink = new TenantJoinLink
                {
                    TenantId = tenantId,
                    JoinToken = Guid.NewGuid().ToString("N"), // 32-char hex string
                    ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpirationDays),
                    DefaultRoleId = dto.DefaultRoleId,
                    IsRevoked = false,
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.TenantJoinLinks.Add(joinLink);
                await _context.SaveChangesAsync();
                
                var result = _mapper.Map<TenantJoinLinkDto>(joinLink);
                _logger.LogInformation($"Created join link {joinLink.Id} for tenant {tenantId}");
                
                return new ApiResponse<TenantJoinLinkDto>(result, true, "Join link created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating join link: {ex.Message}", ex);
                return new ApiResponse<TenantJoinLinkDto>(
                    default!,
                    false,
                    "Failed to create join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> RevokeAsync(int id)
        {
            try
            {
                var joinLink = await _dbSet.FirstOrDefaultAsync(j => j.Id == id);
                if (joinLink == null)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Join link not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }
                
                joinLink.IsRevoked = true;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Revoked join link {id}");
                return new ApiResponse<bool>(true, true, "Join link revoked successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error revoking join link: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to revoke join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<TenantJoinLinkDto>> GetByTokenAsync(string token)
        {
            try
            {
                // Cross-tenant: no tenant filtering
                var joinLink = await _context.TenantJoinLinks
                    .Include(j => j.Tenant)
                    .FirstOrDefaultAsync(j => j.JoinToken == token);
                
                if (joinLink == null)
                {
                    return new ApiResponse<TenantJoinLinkDto>(
                        default!,
                        false,
                        "Join link not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }
                
                var dto = _mapper.Map<TenantJoinLinkDto>(joinLink);
                return new ApiResponse<TenantJoinLinkDto>(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting join link by token: {ex.Message}", ex);
                return new ApiResponse<TenantJoinLinkDto>(
                    default!,
                    false,
                    "Failed to retrieve join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }
    }
}
```

#### Step 4: Create Controller

**File**: `GroundUp.api/Controllers/TenantJoinLinkController.cs`

```csharp
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/tenant-join-links")]
    [ApiController]
    [Authorize] // Requires authentication
    public class TenantJoinLinkController : ControllerBase
    {
        private readonly ITenantJoinLinkRepository _repository;
        private readonly ILoggingService _logger;

        public TenantJoinLinkController(ITenantJoinLinkRepository repository, ILoggingService logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// List join links for current tenant
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<TenantJoinLinkDto>>>> GetAll(
            [FromQuery] FilterParams filterParams,
            [FromQuery] bool includeRevoked = false)
        {
            var result = await _repository.GetAllAsync(filterParams, includeRevoked);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get join link by ID (tenant-scoped)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<TenantJoinLinkDto>>> GetById(int id)
        {
            var result = await _repository.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Create new join link for current tenant
        /// Requires admin permission
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TenantJoinLinkDto>>> Create([FromBody] CreateTenantJoinLinkDto dto)
        {
            // TODO: Add admin check via UserTenant.IsAdmin or permission system
            var result = await _repository.CreateAsync(dto);
            
            // Add full join URL to response
            if (result.Success && result.Data != null)
            {
                result.Data.JoinUrl = $"{Request.Scheme}://{Request.Host}/api/join/{result.Data.JoinToken}";
            }
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Revoke a join link (tenant-scoped)
        /// Requires admin permission
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> Revoke(int id)
        {
            // TODO: Add admin check
            var result = await _repository.RevokeAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
```

#### Step 5: Update AutoMapper

**File**: `GroundUp.infrastructure/mappings/MappingProfile.cs`

Add to constructor:
```csharp
// TenantJoinLink mappings
CreateMap<TenantJoinLink, TenantJoinLinkDto>()
    .ForMember(dest => dest.JoinUrl, opt => opt.Ignore()); // Set by controller
```

#### Step 6: Register in DI

**File**: `GroundUp.infrastructure/extensions/ServiceCollectionExtensions.cs`

Add to service registration section:
```csharp
services.AddScoped<ITenantJoinLinkRepository, TenantJoinLinkRepository>();
```

---

### Priority 2: Remove UserKeycloakIdentity Files (Cleanup)

**Goal**: Clean up unused UserKeycloakIdentity code now that we use `UserTenant.ExternalUserId`

#### Files to Delete:
1. `GroundUp.core/entities/UserKeycloakIdentity.cs`
2. `GroundUp.core/interfaces/IUserKeycloakIdentityRepository.cs`
3. `GroundUp.infrastructure/repositories/UserKeycloakIdentityRepository.cs`
4. `GroundUp.core/entities/AccountLinkToken.cs` (if not needed)

#### Files to Update:

**1. ApplicationDbContext.cs**

Remove these lines:
```csharp
// REMOVE:
public DbSet<UserKeycloakIdentity> UserKeycloakIdentities { get; set; }
public DbSet<AccountLinkToken> AccountLinkTokens { get; set; }

// In OnModelCreating, remove any UserKeycloakIdentity configuration:
// modelBuilder.Entity<UserKeycloakIdentity>()...
```

**2. User.cs entity**

Remove this navigation property (if it exists):
```csharp
// REMOVE (if present):
public ICollection<UserKeycloakIdentity> KeycloakIdentities { get; set; }
```

#### Create EF Migration:

```sh
# In project root
dotnet ef migrations add RemoveUserKeycloakIdentity --project GroundUp.infrastructure --startup-project GroundUp.api

# Review migration file to ensure it drops UserKeycloakIdentities and AccountLinkTokens tables

# Apply migration
dotnet ef database update --project GroundUp.infrastructure --startup-project GroundUp.api
```

#### Verification:

```sh
# Verify no code references exist (should return no results):
grep -r "IUserKeycloakIdentityRepository" --include="*.cs" . | grep -v "Migration"
grep -r "UserKeycloakIdentityRepository" --include="*.cs" . | grep -v "Migration"
grep -r "AccountLinkToken" --include="*.cs" . | grep -v "Migration"
```

---

### Priority 3: Test All Flows End-to-End

**Test Scenarios** (use Postman, curl, or automated tests):

#### 1. Join Link Creation & Usage
```sh
# 1. Create join link (as tenant admin)
POST http://localhost:5000/api/tenant-join-links
Authorization: Bearer {admin-token}
{
  "expirationDays": 30
}
# Note the joinUrl from response

# 2. Visit join link (as new user)
GET {joinUrl}
# Should redirect to Keycloak

# 3. Authenticate/register in Keycloak

# 4. Verify callback creates User and UserTenant with ExternalUserId
# Check database: SELECT * FROM UserTenants WHERE ExternalUserId IS NOT NULL

# 5. Verify user has tenant access (not admin)
GET http://localhost:5000/api/auth/me
Authorization: Bearer {user-token}
```

#### 2. Standard Invitation Flow
```sh
# 1. Create invitation
POST http://localhost:5000/api/tenant-invitations
{
  "email": "user@example.com",
  "isAdmin": false
}

# 2. Visit invitation URL
GET http://localhost:5000/api/invitations/invite/{token}

# 3. Complete authentication

# 4. Verify UserTenant created with ExternalUserId
```

#### 3. Enterprise Invitation Flow
```sh
# 1. Enterprise signup
POST http://localhost:5000/api/tenants/enterprise/signup
{
  "companyName": "Acme Corp",
  "contactEmail": "admin@acme.com",
  "customDomain": "acme.example.com"
}
# Note invitation token from response

# 2. Visit enterprise invitation URL
GET http://localhost:5000/api/invitations/enterprise/invite/{token}

# 3. Complete authentication in enterprise realm

# 4. Verify admin UserTenant created with ExternalUserId
```

---

## Success Criteria

? Join-link CRUD endpoints work (create, list, revoke)  
? Join link flow creates UserTenant with ExternalUserId  
? All invitation flows tested and working  
? UserKeycloakIdentity files removed, migration applied  
? Build succeeds with no errors  
? Database has no UserKeycloakIdentities or AccountLinkTokens tables  
? Ready for email service implementation (Phase 4)

---

## Build & Test Commands

```sh
# Build solution
dotnet build

# Run API
dotnet run --project GroundUp.api

# Create migration (if needed)
dotnet ef migrations add RemoveUserKeycloakIdentity --project GroundUp.infrastructure --startup-project GroundUp.api

# Apply migration
dotnet ef database update --project GroundUp.infrastructure --startup-project GroundUp.api

# Verify no references
grep -r "IUserKeycloakIdentityRepository" --include="*.cs" . | grep -v "Migration"
```

---

## Next Phase: Email Service (Phase 4)

After this phase is complete, we'll implement:
1. Email service interface and implementation (AWS SES or SMTP)
2. Email templates (HTML or Razor)
3. Update enterprise signup to send invitation emails
4. Update standard invitation creation to send emails
5. System settings for email configuration

---

**Start with Join-Link CRUD implementation, then cleanup UserKeycloakIdentity files.**  
**Test all flows thoroughly before moving to email service.**  
**Keep changes small and run build after each step.**
