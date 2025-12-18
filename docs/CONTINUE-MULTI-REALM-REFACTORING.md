# Continue Multi-Realm Implementation - Refactoring Phase

**Context:** We're refactoring the TenantController to move business logic from the controller layer to the repository layer, following proper separation of concerns.

---

## **Current State**

We've implemented multi-realm architecture for GroundUp across 6 phases:

? **Phase 1-5:** Complete (Database, DTOs, Interfaces, Repositories, Services)  
?? **Phase 6:** In progress - Refactoring controller logic to repository

---

## **Problem Identified**

The `TenantController.Create` method has too much business logic:
- Validates enterprise tenant requirements
- Creates Keycloak realms
- Handles rollback on failure
- Complex error handling

**This violates separation of concerns** - controllers should be thin pass-throughs to repositories.

---

## **What We're Doing**

Moving all business logic from `TenantController.Create` to `TenantRepository.AddAsync`:

### **Before (BAD):**
```csharp
// TenantController - 80+ lines of business logic
[HttpPost]
public async Task<ActionResult<ApiResponse<TenantDto>>> Create([FromBody] CreateTenantDto dto)
{
    // Validation
    // Keycloak realm creation
    // Database creation
    // Rollback logic
    // Error handling
}
```

### **After (GOOD):**
```csharp
// TenantController - Simple pass-through
[HttpPost]
public async Task<ActionResult<ApiResponse<TenantDto>>> Create([FromBody] CreateTenantDto dto)
{
    var result = await _tenantRepository.AddAsync(dto);
    return StatusCode(result.StatusCode, result);
}

// TenantRepository.AddAsync - Contains all business logic
public async Task<ApiResponse<TenantDto>> AddAsync(CreateTenantDto dto)
{
    // All validation, Keycloak realm creation, rollback, etc.
}
```

---

## **What's Been Done So Far**

1. ? Updated `TenantRepository.AddAsync` to include:
   - Enterprise tenant validation (RealmUrl required)
   - Keycloak realm creation logic (stubbed with TODO)
   - Database tenant creation
   - Rollback on failure (delete Keycloak realm if DB fails)

2. ?? **Stub methods created** (need to be wired up):
   ```csharp
   private async Task<bool> CreateKeycloakRealmAsync(CreateRealmDto dto)
   {
       // TODO: Inject IIdentityProviderAdminService in constructor
       // Currently returns true (stub)
   }

   private async Task DeleteKeycloakRealmAsync(string realmName)
   {
       // TODO: Inject IIdentityProviderAdminService in constructor
       // Currently does nothing (stub)
   }
   ```

---

## **Next Steps to Continue**

### **Step 1: Wire Up IIdentityProviderAdminService in TenantRepository**

**File:** `GroundUp.infrastructure/repositories/TenantRepository.cs`

Update the constructor:

```csharp
public class TenantRepository : ITenantRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILoggingService _logger;
    private readonly IIdentityProviderAdminService _identityProviderAdminService; // ADD THIS

    public TenantRepository(
        ApplicationDbContext context,
        IMapper mapper,
        ILoggingService logger,
        IIdentityProviderAdminService identityProviderAdminService) // ADD THIS
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
        _identityProviderAdminService = identityProviderAdminService; // ADD THIS
    }
```

Update the stub methods:

```csharp
private async Task<bool> CreateKeycloakRealmAsync(CreateRealmDto dto)
{
    return await _identityProviderAdminService.CreateRealmAsync(dto);
}

private async Task DeleteKeycloakRealmAsync(string realmName)
{
    await _identityProviderAdminService.DeleteRealmAsync(realmName);
}
```

---

### **Step 2: Simplify TenantController.Create**

**File:** `GroundUp.api/Controllers/TenantController.cs`

Replace the entire `Create` method with:

```csharp
/// <summary>
/// Create a new tenant
/// For enterprise tenants, also creates a dedicated Keycloak realm
/// </summary>
[HttpPost]
public async Task<ActionResult<ApiResponse<TenantDto>>> Create([FromBody] CreateTenantDto dto)
{
    if (dto == null)
    {
        return BadRequest(new ApiResponse<TenantDto>(
            default!,
            false,
            "Invalid tenant data.",
            null,
            StatusCodes.Status400BadRequest,
            ErrorCodes.ValidationFailed
        ));
    }

    var result = await _tenantRepository.AddAsync(dto);
    return StatusCode(result.StatusCode, result);
}
```

Remove these from the controller constructor (no longer needed):
```csharp
// REMOVE THESE:
private readonly IIdentityProviderAdminService _identityProviderAdminService;
// Remove from constructor parameters too
```

Update constructor to:
```csharp
public TenantController(
    ITenantRepository tenantRepository,
    ILoggingService logger)
{
    _tenantRepository = tenantRepository;
    _logger = logger;
}
```

---

### **Step 3: Update Delete Method (Same Pattern)**

**File:** `GroundUp.api/Controllers/TenantController.cs`

The `Delete` method has the same TODO. Move that logic to the repository too:

**Repository:**
```csharp
public async Task<ApiResponse<bool>> DeleteAsync(int id)
{
    try
    {
        var tenant = await _context.Tenants.FindAsync(id);
        if (tenant == null)
        {
            return new ApiResponse<bool>(
                false,
                false,
                "Tenant not found",
                null,
                StatusCodes.Status404NotFound,
                ErrorCodes.NotFound
            );
        }

        // Check if tenant has child tenants
        var hasChildren = await _context.Tenants.AnyAsync(t => t.ParentTenantId == id);
        if (hasChildren)
        {
            return new ApiResponse<bool>(
                false,
                false,
                "Cannot delete tenant with child tenants",
                null,
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationFailed
            );
        }

        // Check if tenant has users
        var hasUsers = await _context.UserTenants.AnyAsync(ut => ut.TenantId == id);
        if (hasUsers)
        {
            return new ApiResponse<bool>(
                false,
                false,
                "Cannot delete tenant with assigned users",
                null,
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationFailed
            );
        }

        // If enterprise tenant, delete Keycloak realm first
        if (tenant.IsEnterprise)
        {
            _logger.LogInformation($"Deleting Keycloak realm for enterprise tenant: {tenant.Name.ToLowerInvariant()}");
            var realmDeleted = await DeleteKeycloakRealmAsync(tenant.Name.ToLowerInvariant());
            
            if (!realmDeleted)
            {
                _logger.LogWarning($"Failed to delete Keycloak realm {tenant.Name.ToLowerInvariant()}, but continuing with tenant deletion");
                // Continue anyway - realm might not exist
            }
        }

        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Tenant {id} deleted successfully");

        return new ApiResponse<bool>(
            true,
            true,
            "Tenant deleted successfully",
            null,
            StatusCodes.Status200OK
        );
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error deleting tenant {id}: {ex.Message}", ex);
        return new ApiResponse<bool>(
            false,
            false,
            "An error occurred while deleting the tenant.",
            new List<string> { ex.Message },
            StatusCodes.Status500InternalServerError,
            ErrorCodes.InternalServerError
        );
    }
}
```

**Update helper method to return bool:**
```csharp
private async Task<bool> DeleteKeycloakRealmAsync(string realmName)
{
    return await _identityProviderAdminService.DeleteRealmAsync(realmName);
}
```

**Controller stays simple:**
```csharp
[HttpDelete("{id:int}")]
public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
{
    var result = await _tenantRepository.DeleteAsync(id);
    return StatusCode(result.StatusCode, result);
}
```

---

### **Step 4: Verify and Test**

1. **Build the solution:**
   ```bash
   dotnet build
   ```

2. **Test tenant creation:**
   - Create standard tenant (no realm)
   - Create enterprise tenant (with realm)
   - Test rollback (create tenant with duplicate name)

3. **Test tenant deletion:**
   - Delete standard tenant
   - Delete enterprise tenant (should delete realm too)

---

## **Key Files to Modify**

1. **GroundUp.infrastructure/repositories/TenantRepository.cs**
   - Add `IIdentityProviderAdminService` to constructor
   - Wire up the two stub methods
   - Update `DeleteAsync` to handle enterprise realm deletion

2. **GroundUp.api/Controllers/TenantController.cs**
   - Simplify `Create` method to 1-liner
   - Remove `IIdentityProviderAdminService` from controller
   - Simplify `Delete` method to 1-liner

---

## **Why This Matters**

? **Reusability** - Repository can be used from other contexts (background jobs, CLI tools, etc.)  
? **Testability** - Business logic can be unit tested without HTTP concerns  
? **Maintainability** - Single source of truth for tenant creation/deletion logic  
? **Consistency** - Follows the same pattern as all other repositories  

---

## **Current Issue**

The controller currently has **too much responsibility**. It knows about:
- Keycloak realm creation
- Rollback logic
- Enterprise tenant validation

The repository should handle all of this. The controller should only:
- Validate HTTP input (null check)
- Call repository
- Return HTTP response

---

## **Additional Context**

- **Multi-realm architecture is working** - All phases 1-5 are complete
- **Database migration applied** - TenantType and RealmUrl columns exist
- **Services implemented** - IdentityProviderAdminService has realm management methods
- **This is just refactoring** - Moving code to the right layer

---

## **Questions to Address**

1. Should we also handle tenant deletion rollback? (If DB delete fails after realm delete)
2. Do we want to make realm deletion optional/configurable? (Keep realm but deactivate tenant)
3. Should we log realm deletion to an audit table?

---

## **Related Documentation**

- `docs/MULTI-REALM-API-CHANGES.md` - Full multi-realm architecture spec
- `docs/TENANT-REPOSITORY-REFACTORING.md` - TenantRepository refactoring plan
- `docs/TENANT-MANAGEMENT-SUMMARY.md` - Tenant management overview

---

**Status:** Ready to wire up `IIdentityProviderAdminService` in `TenantRepository` and simplify the controller.

**Next Action:** Update `TenantRepository` constructor and stub methods, then simplify `TenantController`.
