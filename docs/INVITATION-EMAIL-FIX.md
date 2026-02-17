# Invitation Email Fix - Execute Actions Email Not Being Sent

## Problem

When creating invitations for enterprise tenant users, Keycloak was not sending the execute-actions email (password setup + email verification). This prevented invited users from setting up their accounts.

## Root Cause

In `IdentityProviderAdminService.cs`, the `CreateUserAsync` method was setting `requiredActions` in the user creation payload:

```csharp
var userPayload = new
{
    // ...other fields...
    requiredActions = new List<string> { "UPDATE_PASSWORD", "VERIFY_EMAIL" }
};
```

**The problem:** Keycloak does **NOT** automatically send execute-actions emails when you create a user with `requiredActions` set. The `requiredActions` field only marks which actions the user must complete on their next login - it doesn't trigger any email.

To send the execute-actions email, you **must** explicitly call the `/admin/realms/{realm}/users/{userId}/execute-actions-email` endpoint, which is what `SendExecuteActionsEmailAsync` does.

## Solution

**Removed the `requiredActions` field from the user creation payload** in `IdentityProviderAdminService.CreateUserAsync()`. 

Now the flow is:
1. Create user with basic info (no requiredActions)
2. Explicitly call `SendExecuteActionsEmailAsync` with the desired actions
3. Keycloak sends the execute-actions email with the "Back to application" link

## Code Changes

### GroundUp.infrastructure/services/IdentityProviderAdminService.cs

```csharp
// BEFORE (BROKEN - no email sent):
var userPayload = new
{
    username = dto.Username,
    email = dto.Email,
    firstName = dto.FirstName,
    lastName = dto.LastName,
    enabled = dto.Enabled,
    emailVerified = dto.EmailVerified,
    attributes = dto.Attributes ?? new Dictionary<string, List<string>>(),
    requiredActions = new List<string> { "UPDATE_PASSWORD", "VERIFY_EMAIL" } // ? This doesn't trigger email!
};

// AFTER (FIXED - email sent via explicit call):
var userPayload = new
{
    username = dto.Username,
    email = dto.Email,
    firstName = dto.FirstName,
    lastName = dto.LastName,
    enabled = dto.Enabled,
    emailVerified = dto.EmailVerified,
    attributes = dto.Attributes ?? new Dictionary<string, List<string>>()
    // ? No requiredActions - we'll send execute-actions email explicitly
};
```

## Testing

1. **Create an enterprise tenant** (if you don't have one):
   ```bash
   POST /api/tenant/enterprise/signup
   {
     "companyName": "Test Company",
     "adminEmail": "admin@testcompany.com",
     "customDomain": "testcompany.groundup.com",
     "plan": "Professional"
   }
   ```

2. **Create an invitation**:
   ```bash
   POST /api/invitations
   Headers: X-Tenant-Id: {your-tenant-id}
   {
     "email": "newuser@testcompany.com",
     "isAdmin": false,
     "expirationDays": 7
   }
   ```

3. **Check logs** - you should see:
   ```
   Creating user in realm enterprise-testcompany: newuser@testcompany.com
   Successfully created user {userId} in realm enterprise-testcompany
   Sending execute actions email to user {userId} in realm enterprise-testcompany
   Actions: UPDATE_PASSWORD, VERIFY_EMAIL
   Client ID: groundup-api
   Redirect URI: http://localhost:5123/api/invitations/invite/{token}
   Successfully sent execute actions email to user {userId}
   ```

4. **Check email** - The invited user should receive an email from Keycloak with:
   - Subject: "Update Your Account"
   - Link to set password and verify email
   - After completing actions, "Back to application" link redirects to invitation URL

## Why This Happened

In our previous conversation, we were troubleshooting the redirect URL issue. The code was working before because:
- User creation didn't have `requiredActions` set
- We relied solely on the explicit `SendExecuteActionsEmailAsync` call

At some point, the `requiredActions` was added to the user creation payload (possibly thinking it would help), but this actually broke the email flow because:
- Keycloak ignores the execute-actions endpoint call if the user already has those required actions set during creation
- The user creation with `requiredActions` doesn't send any email

## Related Files

- `GroundUp.infrastructure/services/IdentityProviderAdminService.cs` - Fixed user creation
- `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs` - Calls CreateUserAsync + SendExecuteActionsEmailAsync
- `GroundUp.Core/dtos/UserDtos.cs` - CreateUserDto (note: SendWelcomeEmail property is not used by Keycloak)

## Notes

- The `SendWelcomeEmail` property in `CreateUserDto` is misleading - Keycloak's User Representation API doesn't support this field
- Keycloak has two ways to trigger emails:
  1. **execute-actions-email endpoint** - What we use for invitations
  2. **send-verify-email endpoint** - Only for email verification, not password setup
- The `requiredActions` field in user creation is only for marking actions, not triggering emails
