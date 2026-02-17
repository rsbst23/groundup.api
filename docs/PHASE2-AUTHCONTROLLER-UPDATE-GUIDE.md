# Phase 2: Update AuthController for Multi-Realm Identity

## Quick Start

You've completed **Phase 1** (database schema + repository). Now implement **Phase 2** to make the authentication flow actually use the new identity resolution system.

---

## Current Problem

Right now, `AuthController` still assumes:
```csharp
// ? OLD (single-realm only)
var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
var userGuid = Guid.Parse(userId);

var userExists = await _userRepository.GetByIdAsync(userGuid);
```

This breaks multi-realm support because:
- Keycloak `sub` is only unique **per realm**
- Same user in different realms has different `sub` values
- We need `UserKeycloakIdentities` to map `(realm, sub) ? Users.Id`

---

## What Needs to Change

### **1. AuthController Dependencies**

Add the new repository:
```csharp
public class AuthController : ControllerBase
{
    private readonly IUserKeycloakIdentityRepository _identityRepo; // ? ADD THIS
    private readonly IUserRepository _userRepository;
    // ... other dependencies

    public AuthController(
        IUserKeycloakIdentityRepository identityRepo, // ? ADD THIS
        // ... other dependencies
    )
    {
        _identityRepo = identityRepo;
        // ...
    }
}
```

### **2. Update AuthCallback Method**

Current code (around line 70):
```csharp
// ? OLD
var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
var userExists = await _userRepository.GetByIdAsync(userId);
```

Replace with:
```csharp
// ? NEW
var keycloakUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
if (string.IsNullOrEmpty(keycloakUserId))
{
    return BadRequest("No user ID in token");
}

// Resolve to global GroundUp user ID
var userId = await _identityRepo.ResolveUserIdAsync(realm, keycloakUserId);

if (userId == null)
{
    // First time seeing this identity - create user + mapping
    _logger.LogInformation($"First login for Keycloak user {keycloakUserId} in realm {realm}");
    
    var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
    if (keycloakUser == null)
    {
        return NotFound("User not found in Keycloak");
    }
    
    // Create new global user
    var newUser = new User
    {
        Id = Guid.NewGuid(),
        DisplayName = !string.IsNullOrEmpty(keycloakUser.FirstName)
            ? $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim()
            : keycloakUser.Username,
        Email = keycloakUser.Email,           // May be null
        Username = keycloakUser.Username,     // May be null
        FirstName = keycloakUser.FirstName,
        LastName = keycloakUser.LastName,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
    
    var createResult = await _userRepository.AddAsync(newUser);
    if (!createResult.Success)
    {
        return StatusCode(500, "Failed to create user");
    }
    
    // Create identity mapping
    var identityResult = await _identityRepo.CreateIdentityMappingAsync(
        newUser.Id, realm, keycloakUserId);
        
    if (!identityResult.Success)
    {
        return StatusCode(500, "Failed to create identity mapping");
    }
    
    userId = newUser.Id;
}
else
{
    // Existing user - update last login
    var user = await _userRepository.GetByIdAsync(userId.ToString());
    if (user.Success && user.Data != null)
    {
        // Update last login timestamp (TODO: implement UpdateAsync in UserRepository)
    }
}

// Continue with rest of auth flow using userId
_logger.LogInformation($"User {userId} authenticated via realm {realm}");
```

### **3. Update Helper Methods**

**HandleInvitationFlowAsync**: Already uses `userId` (string) ? convert to Guid  
**HandleNewOrganizationFlowAsync**: Already uses `userId` (string) ? convert to Guid  
**HandleDefaultFlowAsync**: Already uses `userId` (string) ? convert to Guid

Just change:
```csharp
var userTenants = await _userTenantRepository.GetTenantsForUserAsync(Guid.Parse(userId));
```

No other changes needed - the `userId` is now the global GroundUp user ID.

---

## Testing Checklist

### **Standard Tenant (Shared Realm)**

1. [ ] First login creates:
   - User record
   - UserKeycloakIdentity mapping (realm='groundup', keycloakUserId=sub)
   
2. [ ] Second login:
   - Resolves existing user via UserKeycloakIdentities
   - Does not create duplicate user
   
3. [ ] New org flow:
   - Creates user + mapping
   - Creates tenant
   - Assigns user to tenant as admin

4. [ ] Invitation flow:
   - Creates user + mapping (if new)
   - Accepts invitation
   - Assigns user to tenant

### **Enterprise Tenant (Future)**

5. [ ] First login to enterprise realm:
   - Resolves existing user (if they were in shared realm)
   - Creates new identity mapping for enterprise realm
   - OR creates new user if completely new
   
6. [ ] Cross-realm user:
   - Has 2 entries in UserKeycloakIdentities (groundup + tenant_acme_1234)
   - Both point to same Users.Id

---

## Common Errors and Fixes

### **"User not found in UserRepository"**

This means you're still trying to look up users by Keycloak `sub` instead of global `Users.Id`.

Fix:
```csharp
// ? WRONG
var user = await _userRepository.GetByIdAsync(keycloakUserId);

// ? CORRECT
var userId = await _identityRepo.ResolveUserIdAsync(realm, keycloakUserId);
var user = await _userRepository.GetByIdAsync(userId.ToString());
```

### **"Duplicate identity mapping"**

This means you're trying to create a mapping that already exists. Check:
```csharp
var existing = await _identityRepo.IdentityExistsAsync(realm, keycloakUserId);
if (!existing)
{
    await _identityRepo.CreateIdentityMappingAsync(userId, realm, keycloakUserId);
}
```

### **"Null reference on User.Email"**

User.Email is now nullable. Always check:
```csharp
if (!string.IsNullOrEmpty(user.Email))
{
    // Use email
}
else
{
    // Use DisplayName or Username
}
```

---

## Files to Modify

1. **GroundUp.api/Controllers/AuthController.cs**
   - Add `IUserKeycloakIdentityRepository` dependency
   - Update `AuthCallback` method
   - Update user creation logic

2. **GroundUp.infrastructure/repositories/UserRepository.cs** (if needed)
   - Add `UpdateAsync` method to update LastLoginAt

3. **GroundUp.Core/dtos/UserDtos.cs** (if needed)
   - Create `UpdateUserDto` if it doesn't exist

---

## Next Steps After Phase 2

Once AuthController is updated:

- **Phase 3**: Implement enterprise tenant provisioning endpoint
- **Phase 4**: Add realm resolution logic (from domain, invitation token)
- **Phase 5**: Build account linking UI and endpoints
- **Phase 6**: User discovery for standard tenants

---

## Quick Commands

### Run Migration (if not done yet)
```bash
dotnet ef database update --project GroundUp.infrastructure --startup-project GroundUp.api
```

### Test Build
```bash
dotnet build
```

### Run Application
```bash
cd GroundUp.api
dotnet run
```

---

## Reference Files

- Design Doc: `groundup-auth-architecture.md`
- Phase 1 Summary: `docs/MULTI-REALM-IDENTITY-PHASE1-COMPLETE.md`
- Identity Repository: `GroundUp.infrastructure/repositories/UserKeycloakIdentityRepository.cs`
- Interface: `GroundUp.Core/interfaces/IUserKeycloakIdentityRepository.cs`

---

**Ready to start? Focus on updating `AuthController.cs` first!**
