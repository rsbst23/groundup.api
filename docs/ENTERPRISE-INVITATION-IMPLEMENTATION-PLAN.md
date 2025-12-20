# Enterprise Invitation Implementation Plan

## Overview
This document outlines the changes needed to implement the invitation flow as specified in `enterprise-tenant-invitations.md`.

## Current State

### What Works
- Basic invitation creation (DB record only)
- Invitation acceptance flow
- Email validation and token handling
- Membership creation

### What's Missing
1. **`accountType` field** - Invitations don't distinguish between local/SSO accounts
2. **Keycloak user creation** - System doesn't create Keycloak users during invitation
3. **Execute actions email** - System doesn't trigger Keycloak's password setup email
4. **Token hashing** - Raw tokens are stored (security issue)

## Required Changes

### 1. Database Schema Changes

**Add to `TenantInvitation` entity:**
```csharp
// Account type: "local" or "sso"
[MaxLength(10)]
public string AccountType { get; set; } = "local";

// Store hashed token instead of raw token
[MaxLength(64)] // SHA256 hash
public string TokenHash { get; set; } = string.Empty;

// Store Keycloak realm for this invitation
[MaxLength(255)]
public string KeycloakRealm { get; set; } = string.Empty;
```

### 2. DTO Changes

**Update `CreateTenantInvitationDto`:**
```csharp
[Required]
public string AccountType { get; set; } = "local"; // "local" or "sso"

[Range(1, 8760)] // Up to 1 year
public int ExpiresInHours { get; set; } = 72;

// Optional: roles/policies to assign
public List<string>? Roles { get; set; }
public List<string>? Policies { get; set; }
```

**Update `TenantInvitationDto`:**
```csharp
public string AccountType { get; set; } = string.Empty;
public string KeycloakRealm { get; set; } = string.Empty;
```

### 3. IIdentityProviderAdminService Changes

**Add new methods:**
```csharp
/// <summary>
/// Creates a new user in Keycloak with required actions
/// </summary>
/// <param name="realm">The realm to create user in</param>
/// <param name="dto">User creation details</param>
/// <returns>Created user ID (sub claim) or null if failed</returns>
Task<string?> CreateUserAsync(string realm, CreateUserDto dto);

/// <summary>
/// Sends Keycloak execute actions email (password setup, email verification, etc.)
/// </summary>
/// <param name="realm">The realm</param>
/// <param name="userId">The Keycloak user ID</param>
/// <param name="actions">Required actions (e.g., ["UPDATE_PASSWORD", "VERIFY_EMAIL"])</param>
/// <param name="redirectUri">Optional URI to redirect to after completing actions</param>
/// <returns>True if email sent successfully</returns>
Task<bool> SendExecuteActionsEmailAsync(
    string realm, 
    string userId, 
    List<string> actions, 
    string? redirectUri = null);

/// <summary>
/// Checks if a user exists in Keycloak by email
/// </summary>
/// <param name="realm">The realm to search</param>
/// <param name="email">The email address</param>
/// <returns>User ID if found, null otherwise</returns>
Task<string?> GetUserIdByEmailAsync(string realm, string email);
```

### 4. TenantInvitationRepository Changes

**Update `AddAsync` method:**

```csharp
public async Task<ApiResponse<TenantInvitationDto>> AddAsync(
    CreateTenantInvitationDto dto, 
    Guid createdByUserId)
{
    // 1. Validate and get tenant
    var tenantId = _tenantContext.TenantId;
    var tenant = await _context.Tenants.FindAsync(tenantId);
    
    // Get realm name for invitation
    var realmName = tenant.RealmName;
    
    // 2. Generate raw token (return to caller, never store)
    var rawToken = Guid.NewGuid().ToString("N");
    var tokenHash = HashToken(rawToken);
    
    // 3. Create invitation record
    var invitation = new TenantInvitation
    {
        ContactEmail = dto.Email.ToLowerInvariant(),
        TenantId = tenantId,
        TokenHash = tokenHash, // ?? Store hash, not raw
        KeycloakRealm = realmName,
        AccountType = dto.AccountType,
        ExpiresAt = DateTime.UtcNow.AddHours(dto.ExpiresInHours),
        Status = InvitationStatus.Pending,
        IsAdmin = dto.IsAdmin,
        CreatedByUserId = createdByUserId,
        CreatedAt = DateTime.UtcNow
    };
    
    _context.TenantInvitations.Add(invitation);
    await _context.SaveChangesAsync();
    
    // 4. Handle account type specific logic
    if (dto.AccountType == "local")
    {
        await HandleLocalAccountInvitationAsync(
            invitation, 
            realmName, 
            rawToken);
    }
    else if (dto.AccountType == "sso")
    {
        // For SSO: Just return the token
        // Frontend/email service will send invitation email with link
    }
    
    // 5. Return DTO with raw token (for email link construction)
    var invitationDto = _mapper.Map<TenantInvitationDto>(invitation);
    invitationDto.InvitationToken = rawToken; // ?? Return raw token for email
    
    return new ApiResponse<TenantInvitationDto>(invitationDto);
}

private async Task HandleLocalAccountInvitationAsync(
    TenantInvitation invitation,
    string realmName,
    string rawToken)
{
    // 1. Check if user exists in Keycloak
    var existingUserId = await _identityProvider.GetUserIdByEmailAsync(
        realmName, 
        invitation.ContactEmail);
    
    string keycloakUserId;
    
    if (existingUserId == null)
    {
        // 2. Create Keycloak user
        var createUserDto = new CreateUserDto
        {
            Username = invitation.ContactEmail.Split('@')[0], // Use email prefix
            Email = invitation.ContactEmail,
            FirstName = invitation.ContactName?.Split(' ').FirstOrDefault(),
            LastName = invitation.ContactName?.Split(' ').Skip(1).FirstOrDefault(),
            Enabled = true,
            EmailVerified = false
        };
        
        keycloakUserId = await _identityProvider.CreateUserAsync(
            realmName, 
            createUserDto);
        
        if (keycloakUserId == null)
        {
            throw new Exception("Failed to create Keycloak user");
        }
        
        _logger.LogInformation(
            $"Created Keycloak user {keycloakUserId} for invitation {invitation.Id}");
    }
    else
    {
        keycloakUserId = existingUserId;
        _logger.LogInformation(
            $"Using existing Keycloak user {keycloakUserId} for invitation {invitation.Id}");
    }
    
    // 3. Send execute actions email from Keycloak
    var actions = new List<string> 
    { 
        "UPDATE_PASSWORD", 
        "VERIFY_EMAIL" 
    };
    
    // Build redirect URI with invitation token
    var redirectUri = $"https://{Request.Host}/accept-invite?token={rawToken}";
    
    var emailSent = await _identityProvider.SendExecuteActionsEmailAsync(
        realmName,
        keycloakUserId,
        actions,
        redirectUri);
    
    if (!emailSent)
    {
        _logger.LogWarning(
            $"Failed to send execute actions email for invitation {invitation.Id}");
    }
}

private string HashToken(string rawToken)
{
    using var sha256 = System.Security.Cryptography.SHA256.Create();
    var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawToken));
    return Convert.ToBase64String(hashBytes);
}
```

**Update `GetByTokenAsync` method:**
```csharp
public async Task<ApiResponse<TenantInvitationDto>> GetByTokenAsync(string rawToken)
{
    var tokenHash = HashToken(rawToken);
    
    var invitation = await _context.TenantInvitations
        .Include(ti => ti.Tenant)
        .Include(ti => ti.CreatedByUser)
        .Include(ti => ti.AcceptedByUser)
        .FirstOrDefaultAsync(ti => ti.TokenHash == tokenHash);
    
    // ... rest of logic
}
```

**Update `AcceptInvitationAsync` method:**
```csharp
public async Task<ApiResponse<bool>> AcceptInvitationAsync(
    string rawToken, 
    Guid userId, 
    string? externalUserId = null)
{
    var tokenHash = HashToken(rawToken);
    
    var invitation = await _context.TenantInvitations
        .Include(ti => ti.Tenant)
        .FirstOrDefaultAsync(ti => ti.TokenHash == tokenHash);
    
    // ... existing validation logic
    
    // Additional validation: realm check
    var authenticatedRealm = GetRealmFromContext(); // From JWT issuer
    if (invitation.KeycloakRealm != authenticatedRealm)
    {
        return new ApiResponse<bool>(
            false,
            false,
            "Realm mismatch - you must authenticate in the correct tenant realm",
            null,
            StatusCodes.Status403Forbidden,
            ErrorCodes.Unauthorized
        );
    }
    
    // ... rest of existing logic
}
```

### 5. IdentityProviderAdminService Implementation

**Add new methods to `IdentityProviderAdminService`:**

```csharp
public async Task<string?> CreateUserAsync(string realm, CreateUserDto dto)
{
    try
    {
        var token = await GetAdminTokenAsync();
        
        var userPayload = new
        {
            username = dto.Username,
            email = dto.Email,
            firstName = dto.FirstName,
            lastName = dto.LastName,
            enabled = dto.Enabled,
            emailVerified = dto.EmailVerified,
            attributes = dto.Attributes ?? new Dictionary<string, List<string>>()
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            $"{_keycloakUrl}/admin/realms/{realm}/users",
            userPayload,
            GetAuthHeaders(token));
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to create user: {error}");
            return null;
        }
        
        // Get user ID from Location header
        var location = response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(location))
        {
            _logger.LogError("No Location header returned after user creation");
            return null;
        }
        
        var userId = location.Split('/').Last();
        return userId;
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error creating user in realm {realm}: {ex.Message}", ex);
        return null;
    }
}

public async Task<bool> SendExecuteActionsEmailAsync(
    string realm,
    string userId,
    List<string> actions,
    string? redirectUri = null)
{
    try
    {
        var token = await GetAdminTokenAsync();
        
        var queryParams = redirectUri != null 
            ? $"?redirect_uri={Uri.EscapeDataString(redirectUri)}"
            : "";
        
        var response = await _httpClient.PutAsJsonAsync(
            $"{_keycloakUrl}/admin/realms/{realm}/users/{userId}/execute-actions-email{queryParams}",
            actions,
            GetAuthHeaders(token));
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to send execute actions email: {error}");
            return false;
        }
        
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error sending execute actions email: {ex.Message}", ex);
        return false;
    }
}

public async Task<string?> GetUserIdByEmailAsync(string realm, string email)
{
    try
    {
        var token = await GetAdminTokenAsync();
        
        var response = await _httpClient.GetAsync(
            $"{_keycloakUrl}/admin/realms/{realm}/users?email={Uri.EscapeDataString(email)}&exact=true",
            GetAuthHeaders(token));
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        
        var users = await response.Content.ReadFromJsonAsync<List<UserDetailsDto>>();
        return users?.FirstOrDefault()?.Id;
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error searching for user by email: {ex.Message}", ex);
        return null;
    }
}
```

### 6. Migration

**Create migration:**
```bash
dotnet ef migrations add AddInvitationAccountTypeAndHashing -p GroundUp.infrastructure -s GroundUp.api
```

**Migration will:**
- Add `AccountType` column (default: "local")
- Add `TokenHash` column
- Add `KeycloakRealm` column
- Migrate existing `InvitationToken` values to `TokenHash` (hash them)
- Keep `InvitationToken` column temporarily for backwards compatibility

## Testing Plan

### Local Account Flow
1. Admin creates invitation with `accountType: "local"`
2. System creates Keycloak user in enterprise realm
3. System sends Keycloak execute actions email
4. User receives ONE email from Keycloak
5. User sets password and verifies email
6. User authenticates with Keycloak
7. Auth callback processes invitation
8. User gains access to tenant

### SSO Account Flow
1. Admin creates invitation with `accountType: "sso"`
2. System creates invitation record only (no Keycloak user)
3. Frontend/email service sends app email with invite link
4. User clicks link and authenticates via enterprise IdP
5. Auth callback processes invitation
6. User gains access to tenant

## Security Considerations

1. **Token hashing**: Never store raw tokens in database
2. **Token entropy**: Use GUID (128-bit) for token generation
3. **Realm isolation**: Validate authenticated realm matches invitation realm
4. **Email validation**: Validate authenticated email matches invitation email
5. **Single-use tokens**: Mark invitation as accepted after use
6. **Expiration**: Enforce time-based expiration

## Rollout Strategy

1. ? Create migration (add new columns, keep old for backwards compat)
2. ? Update interfaces and DTOs
3. ? Implement IIdentityProviderAdminService methods
4. ? Update TenantInvitationRepository
5. ? Update InvitationController if needed
6. ? Test local account flow
7. ? Test SSO account flow
8. ? Update documentation
9. ? Remove old `InvitationToken` column in future migration

## Notes

- The `Request` object isn't available in repository layer - need to pass redirect URI from controller
- Consider adding `IHttpContextAccessor` or passing host info from controller
- Frontend will need to construct the invitation URLs based on returned token
- Consider adding invitation statistics/tracking
