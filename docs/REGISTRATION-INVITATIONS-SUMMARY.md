# Registration-Based Invitations - Quick Summary

## The Problem

Keycloak's execute-actions email doesn't redirect users after password setup ? users get stuck.

## The Solution

**Use Keycloak's REGISTRATION flow instead of pre-creating users.**

## How It Works (30-Second Version)

1. Admin creates invitation ? stored in database (**no Keycloak user created**)
2. User receives email with invitation link
3. Link redirects to **Keycloak registration page**
4. User registers ? Keycloak creates account
5. Keycloak redirects to OAuth callback ? Invitation validated ? User has access

## One-Line Summary

**Instead of creating users and sending execute-actions emails, let users self-register via Keycloak's registration form with invitation token in OAuth state.**

## Code Changes

### Before
```csharp
// Create user
var userId = await _identityProvider.CreateUserAsync(realm, dto);

// Send execute-actions email
await _identityProvider.SendExecuteActionsEmailAsync(
    realm, userId, ["UPDATE_PASSWORD", "VERIFY_EMAIL"], redirectUri
);
// ? User stuck after password setup
```

### After
```csharp
// Just store invitation - don't create user
_logger.LogInformation("Invitation created. User will self-register.");

// TODO: Send ONE email with invitation link
// Link: /api/invitations/invite/{token}
// ? Redirects to Keycloak registration
```

## What Changed

| File | Change |
|------|--------|
| `TenantInvitationRepository.cs` | Don't create Keycloak users |
| `InvitationController.cs` | Return registration URL instead of login URL |
| OAuth callback | No changes (already handles invitation validation) |

## Benefits

- ? ONE email (not two)
- ? No stuck state
- ? Simple implementation
- ? No Keycloak customization
- ? Same flow as first admin

## What's Next

1. Implement email service (60 min)
2. Send invitation email after creation
3. Test end-to-end

## Testing

```bash
# 1. Create invitation
POST /api/tenant-invitations
{
  "email": "test@example.com",
  "isAdmin": false,
  "expirationDays": 7
}

# 2. Get invitation URL
GET /api/invitations/invite/{token}
# Returns: {authUrl: "http://localhost:8080/realms/xyz/protocol/openid-connect/registrations?..."}

# 3. Open URL in browser
# 4. Fill registration form
# 5. Submit ? Auto-login ? Has tenant access ?
```

## Documentation

- **Full Guide:** `docs/REGISTRATION-BASED-INVITATION-FLOW.md`
- **Implementation Status:** `docs/EXECUTE-ACTIONS-REDIRECT-ISSUE.md`

## Comparison

| Approach | Emails | Simple? | Works? |
|----------|--------|---------|--------|
| Execute-actions + redirect | 1 | ? | ? (stuck) |
| Execute-actions + follow-up | 2 | ? | ? |
| **Registration-based** | **1** | **?** | **?** |

---

**TL;DR:** Use registration instead of execute-actions. ONE email, no stuck state, simple implementation.
