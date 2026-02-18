# Multi-Realm Support - IIdentityProviderAdminService Update

**Issue:** The `AuthCallback` method needs to query users from specific Keycloak realms, but `IIdentityProviderAdminService` doesn't support realm parameters.

---

## Problem

In `AuthController.AuthCallback`:

```csharp
// Line 84 - Missing realm parameter!
var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(userId);
```

**Why it matters:**
- User authenticated in realm "acme" (enterprise tenant)
- We try to query user from default realm "groundup"
- User not found ? authentication fails!

**Correct flow:**
```csharp
// Extract realm from state (line 59)
realm = callbackState?.Realm;

// Exchange tokens with that realm (line 63) ?
var tokenResponse = await _identityProviderService.ExchangeCodeForTokensAsync(code, redirectUri, realm);

// Get user from SAME realm (line 84) ? Currently missing!
var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(userId, realm);
```

---

## Solution

Add optional `realm` parameter to all `IIdentityProviderAdminService` methods that interact with realm-specific data.

### Methods Requiring Realm Parameter

#### **User Management** (CRITICAL)
- [x] `GetAllUsersAsync(string? realm = null)`
- [x] `GetUserByIdAsync(string userId, string? realm = null)` ? **CRITICAL FOR AUTH**
- [x] `GetUserByUsernameAsync(string username, string? realm = null)`
- [x] `CreateUserAsync(CreateUserDto userDto, string? realm = null)`
- [x] `UpdateUserAsync(string userId, UpdateUserDto userDto, string? realm = null)`
- [x] `DeleteUserAsync(string userId, string? realm = null)`
- [x] `SetUserEnabledAsync(string userId, bool enabled, string? realm = null)`
- [x] `SendPasswordResetEmailAsync(string userId, string? realm = null)`

#### **Role Management** (For completeness)
- [x] `GetAllRolesAsync(string? realm = null)`
- [x] `GetRoleByNameAsync(string name, string? realm = null)`
- [x] `CreateRoleAsync(CreateSystemRoleDto roleDto, string? realm = null)`
- [x] `UpdateRoleAsync(string name, UpdateRoleDto roleDto, string? realm = null)`
- [x] `DeleteRoleAsync(string name, string? realm = null)`

#### **User-Role Management** (For completeness)
- [x] `GetUserRolesAsync(string userId, string? realm = null)`
- [x] `AssignRoleToUserAsync(string userId, string roleName, string? realm = null)`
- [x] `AssignRolesToUserAsync(string userId, List<string> roleNames, string? realm = null)`
- [x] `RemoveRoleFromUserAsync(string userId, string roleName, string? realm = null)`

#### **Realm Management** (No change needed)
- `CreateRealmAsync(CreateRealmDto dto)` - Creates new realms (not scoped to a realm)
- `DeleteRealmAsync(string realmName)` - Deletes a realm by name
- `GetRealmAsync(string realmName)` - Gets realm by name

---

## Implementation Status

### ? Interface Updated
`GroundUp.Core/interfaces/IIdentityProviderAdminService .cs`

### ? AuthController Updated
`GroundUp.api/Controllers/AuthController.cs` - Line 84 now passes `realm` parameter

### ?? Implementation Partially Updated
`GroundUp.infrastructure/services/IdentityProviderAdminService.cs`
- ? `GetUserByIdAsync` - Added realm support
- ? All other methods - Need realm parameter added

---

## Implementation Pattern

All methods follow this pattern:

```csharp
public async Task<TResult> MethodName(params, string? realm = null)
{
    await EnsureAdminTokenAsync();

    // Use provided realm or default from configuration
    var targetRealm = realm ?? _keycloakConfig.Realm;
    
    var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{targetRealm}/...";
    
    // Rest of implementation...
    
    _logger.LogInformation($"Operation completed in realm {targetRealm}");
}
```

**Key points:**
1. Accept optional `string? realm = null` parameter
2. Use `realm ?? _keycloakConfig.Realm` to get target realm
3. Replace hardcoded `{_keycloakConfig.Realm}` with `{targetRealm}`
4. Log which realm was used

---

## Next Steps

### Option A: Update All Methods Now
Update all 16 methods in `IdentityProviderAdminService` to support realm parameter.

**Pros:**
- Complete multi-realm support
- Future-proof
- Consistent API

**Cons:**
- Larger changeset
- More testing required

### Option B: Update Only Critical Methods
Update only user management methods (8 methods).

**Pros:**
- Smaller changeset
- Fixes immediate auth issue
- Less risk

**Cons:**
- Inconsistent API
- Will need to update role methods later

---

## Recommendation

**Go with Option A** - Update all methods now because:
1. Build is already broken
2. Interface is already updated
3. Pattern is simple and consistent
4. Avoids future breaking changes

---

## Testing Checklist

After implementation:

### Standard Realm Auth
- [ ] User logs in via default realm
- [ ] `GetUserByIdAsync` called without realm parameter
- [ ] Uses default realm ("groundup")
- [ ] User found and synced

### Enterprise Realm Auth
- [ ] User logs in via enterprise realm ("acme")
- [ ] `GetUserByIdAsync` called with realm="acme"
- [ ] Uses provided realm
- [ ] User found and synced

### Error Handling
- [ ] User authenticated in realm A, queried from realm B
- [ ] Returns null (user not found)
- [ ] Proper error message returned

---

## Build Errors to Fix

```
error CS0535: 'IdentityProviderAdminService' does not implement interface member 'IIdentityProviderAdminService.GetAllRolesAsync(string?)'
error CS0535: 'IdentityProviderAdminService' does not implement interface member 'IIdentityProviderAdminService.GetRoleByNameAsync(string, string?)'
... (14 more similar errors)
```

All require adding `string? realm = null` parameter to method signatures.

---

**Status:** Ready to implement  
**Priority:** HIGH (blocks authentication for enterprise tenants)  
**Estimated Time:** 30 minutes
