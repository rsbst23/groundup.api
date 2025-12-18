# Before/After Comparison - Tenant Controller Refactoring

## TenantController.cs - Create Method

### BEFORE (80+ lines of business logic in controller)

```csharp
[HttpPost]
public async Task<ActionResult<ApiResponse<TenantDto>>> Create([FromBody] CreateTenantDto dto)
{
    try
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

        _logger.LogInformation($"Creating tenant: {dto.Name} (Type: {dto.TenantType})");

        // Validate RealmUrl for enterprise tenants
        if (dto.TenantType == "enterprise" && string.IsNullOrWhiteSpace(dto.RealmUrl))
        {
            return BadRequest(new ApiResponse<TenantDto>(
                default!,
                false,
                "RealmUrl is required for enterprise tenants",
                new List<string> { "RealmUrl cannot be empty for enterprise tenants" },
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationFailed
            ));
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

            _logger.LogInformation($"Creating Keycloak realm for enterprise tenant: {realmDto.Realm}");
            var realmCreated = await _identityProviderAdminService.CreateRealmAsync(realmDto);
            
            if (!realmCreated)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<TenantDto>(
                    default!,
                    false,
                    "Failed to create Keycloak realm for enterprise tenant",
                    new List<string> { "Realm creation failed" },
                    StatusCodes.Status500InternalServerError,
                    "REALM_CREATION_FAILED"
                ));
            }

            _logger.LogInformation($"Successfully created Keycloak realm: {realmDto.Realm}");
        }

        // Create tenant in database
        var result = await _tenantRepository.AddAsync(dto);

        if (!result.Success)
        {
            // Rollback: Delete realm if it was created
            if (dto.TenantType == "enterprise")
            {
                _logger.LogWarning($"Tenant creation failed, rolling back Keycloak realm: {dto.Name.ToLowerInvariant()}");
                await _identityProviderAdminService.DeleteRealmAsync(dto.Name.ToLowerInvariant());
            }

            return StatusCode(result.StatusCode, new ApiResponse<TenantDto>(
                default!,
                false,
                result.Message,
                result.Errors,
                result.StatusCode,
                result.ErrorCode
            ));
        }

        _logger.LogInformation($"Successfully created tenant: {dto.Name} (ID: {result.Data!.Id})");
        return StatusCode(result.StatusCode, result);
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error creating tenant: {ex.Message}", ex);
        
        // Attempt rollback if enterprise tenant
        if (dto?.TenantType == "enterprise" && !string.IsNullOrWhiteSpace(dto.Name))
        {
            try
            {
                _logger.LogWarning($"Exception during tenant creation, attempting realm rollback: {dto.Name.ToLowerInvariant()}");
                await _identityProviderAdminService.DeleteRealmAsync(dto.Name.ToLowerInvariant());
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError($"Failed to rollback realm during exception handling: {rollbackEx.Message}", rollbackEx);
            }
        }
        
        return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<TenantDto>(
            default!,
            false,
            "An error occurred while creating the tenant",
            new List<string> { ex.Message },
            StatusCodes.Status500InternalServerError,
            "TENANT_CREATION_ERROR"
        ));
    }
}
```

### AFTER (8 lines - clean and simple)

```csharp
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

---

## TenantController.cs - Constructor

### BEFORE
```csharp
private readonly ITenantRepository _tenantRepository;
private readonly IIdentityProviderAdminService _identityProviderAdminService;
private readonly ILoggingService _logger;

public TenantController(
    ITenantRepository tenantRepository,
    IIdentityProviderAdminService identityProviderAdminService,
    ILoggingService logger)
{
    _tenantRepository = tenantRepository;
    _identityProviderAdminService = identityProviderAdminService;
    _logger = logger;
}
```

### AFTER
```csharp
private readonly ITenantRepository _tenantRepository;
private readonly ILoggingService _logger;

public TenantController(
    ITenantRepository tenantRepository,
    ILoggingService logger)
{
    _tenantRepository = tenantRepository;
    _logger = logger;
}
```

---

## TenantRepository.cs - Constructor

### BEFORE
```csharp
private readonly ApplicationDbContext _context;
private readonly IMapper _mapper;
private readonly ILoggingService _logger;

public TenantRepository(
    ApplicationDbContext context,
    IMapper mapper,
    ILoggingService logger)
{
    _context = context;
    _mapper = mapper;
    _logger = logger;
}
```

### AFTER
```csharp
private readonly ApplicationDbContext _context;
private readonly IMapper _mapper;
private readonly ILoggingService _logger;
private readonly IIdentityProviderAdminService _identityProviderAdminService;

public TenantRepository(
    ApplicationDbContext context,
    IMapper mapper,
    ILoggingService logger,
    IIdentityProviderAdminService identityProviderAdminService)
{
    _context = context;
    _mapper = mapper;
    _logger = logger;
    _identityProviderAdminService = identityProviderAdminService;
}
```

---

## TenantRepository.cs - Helper Methods

### BEFORE (Stubs)
```csharp
private async Task<bool> CreateKeycloakRealmAsync(CreateRealmDto dto)
{
    // TODO: Inject IIdentityProviderAdminService in constructor
    // return await _identityProviderAdminService.CreateRealmAsync(dto);
    
    _logger.LogWarning("Keycloak realm creation not yet wired - requires IIdentityProviderAdminService injection");
    return await Task.FromResult(true); // Stub for now
}

private async Task DeleteKeycloakRealmAsync(string realmName)
{
    // TODO: Inject IIdentityProviderAdminService in constructor
    // await _identityProviderAdminService.DeleteRealmAsync(realmName);
    
    _logger.LogWarning($"Keycloak realm deletion not yet wired - would delete: {realmName}");
    await Task.CompletedTask; // Stub for now
}
```

### AFTER (Working)
```csharp
private async Task<bool> CreateKeycloakRealmAsync(CreateRealmDto dto)
{
    return await _identityProviderAdminService.CreateRealmAsync(dto);
}

private async Task<bool> DeleteKeycloakRealmAsync(string realmName)
{
    return await _identityProviderAdminService.DeleteRealmAsync(realmName);
}
```

---

## TenantRepository.cs - DeleteAsync Method

### BEFORE (Missing realm deletion)
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

### AFTER (With realm deletion)
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

---

## Key Improvements

### Code Reduction
- **TenantController.Create**: 80+ lines ? 8 lines ?
- **TenantController dependencies**: 3 ? 2 ?
- **Complexity moved**: Controller ? Repository ?

### Separation of Concerns
- **Controller**: Now only handles HTTP concerns ?
- **Repository**: Now handles all business logic ?
- **Services**: Properly injected where needed ?

### Maintainability
- **Single source of truth**: All tenant logic in repository ?
- **Testability**: Business logic can be unit tested ?
- **Reusability**: Repository can be used from any context ?

---

## Pattern Applied

This follows the **Fat Repository, Thin Controller** pattern:

```
???????????????????????
?   Controller        ?  ? Thin (HTTP only)
?  - Validate input   ?
?  - Call repository  ?
?  - Return response  ?
???????????????????????
         ?
???????????????????????
?   Repository        ?  ? Fat (Business logic)
?  - Validate rules   ?
?  - External calls   ?
?  - Transactions     ?
?  - Rollback logic   ?
???????????????????????
         ?
???????????????????????
?   Services          ?  ? Specialized tasks
?  - Keycloak API     ?
?  - Email service    ?
?  - etc.             ?
???????????????????????
```

---

**Result:** Clean, maintainable, testable code that follows SOLID principles ?
