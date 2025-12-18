# JWT Claims Collision Fix - COMPLETE

## Problem Identified

When creating tenant invitations, the controller was getting the **wrong user ID** from JWT claims.

### Root Cause

The JWT token contained **TWO** `ClaimTypes.NameIdentifier` claims:

1. **Keycloak's NameIdentifier** (from Keycloak access token): `d8796d95-8dba-4df0-bd24-f33503cba11d` 
2. **GroundUp's NameIdentifier** (added by TokenService): `aa1c527e-dc25-412a-8d2f-1f3bc6e47e51`

When calling `User.FindFirst(ClaimTypes.NameIdentifier)`, it returned the **first** match (Keycloak's ID), causing the foreign key constraint error because that Keycloak ID doesn't exist in the `Users` table.

## Solution Implemented

### 1. **TokenService.cs** - Use Custom Claim Type

Changed from:
```csharp
new Claim(ClaimTypes.NameIdentifier, userId.ToString())
```

To:
```csharp
new Claim("groundup_user_id", userId.ToString()), // Custom claim type
new Claim(ClaimTypes.NameIdentifier, userId.ToString()) // Kept for backwards compatibility
```

### 2. **Controllers Updated** - Use Custom Claim

Updated all controllers to use the custom `groundup_user_id` claim instead of `ClaimTypes.NameIdentifier`:

#### TenantInvitationController.cs
```csharp
// OLD:
var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

// NEW:
var userIdClaim = User.FindFirst("groundup_user_id")?.Value;
```

#### AuthController.cs
- Updated `GetUserProfile()` method
- Updated `SetTenant()` method

#### InvitationController.cs
- Updated `AcceptInvitation()` method

## Files Changed

1. `GroundUp.infrastructure/services/TokenService.cs`
2. `GroundUp.api/Controllers/TenantInvitationController.cs`
3. `GroundUp.api/Controllers/AuthController.cs`
4. `GroundUp.api/Controllers/InvitationController.cs`

## Testing Required

1. **Log out completely** (clear cookies and tokens)
2. **Log in fresh** (to get new JWT token with custom claim)
3. **Try creating a tenant invitation**
4. **Verify the correct user ID is used**

## Why This Works

- **Custom claim type**: `groundup_user_id` is unique and won't collide with Keycloak claims
- **Backwards compatibility**: Kept `ClaimTypes.NameIdentifier` for any existing code that might use it
- **Clear separation**: Keycloak claims stay separate from GroundUp claims

## Verification

After logging in, check the JWT token claims - you should see:
```json
{
  "sub": "d8796d95-8dba-4df0-bd24-f33503cba11d",  // Keycloak's user ID
  "groundup_user_id": "aa1c527e-dc25-412a-8d2f-1f3bc6e47e51",  // GroundUp's user ID
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "aa1c527e-dc25-412a-8d2f-1f3bc6e47e51",
  "tenant_id": "3",
  // ... other claims
}
```

The controller will now correctly extract `groundup_user_id` which matches your `Users` table.

## Next Steps

1. Test the fix by logging in and creating an invitation
2. If successful, consider removing the temporary debug logging from `UserTenantRepository.GetByRealmAndExternalUserIdAsync`
3. Document the custom claim usage for future developers
