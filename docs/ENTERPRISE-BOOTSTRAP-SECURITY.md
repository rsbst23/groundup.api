# Enterprise Realm Bootstrap Security - Design Addition

## Overview

This document details the security measures added to prevent lockout scenarios for enterprise tenants during initial setup.

---

## Problem Statement

**Scenario:**
1. Enterprise customer signs up
2. First admin creates account in dedicated realm
3. Admin forgets password
4. Admin's email was never verified or is invalid
5. **Customer is permanently locked out** - no way to recover

**Why This Matters:**
- Enterprise realms are isolated (no shared admin access)
- First admin is sole access point initially
- Password reset requires verified email
- Support intervention is expensive and damages trust

---

## Solution: Multi-Layer Security

### **Layer 1: Email Verification Requirement**

**Enforce during registration:**
```csharp
var realmConfig = new CreateRealmDto
{
    EmailVerificationRequired = true,  // ? REQUIRED for enterprise realms
    LoginWithEmailAllowed = true,
    RegistrationAllowed = true
};
```

**Block login until verified:**
```csharp
if (invitation.RequiresEmailVerification && !keycloakUser.EmailVerified)
{
    return Unauthorized("Please verify your email before accepting invitation");
}
```

**Benefits:**
- ? Ensures working email for password recovery
- ? Validates contact email is real
- ? Prevents typos in email address

**Trade-off:**
- Adds friction to signup flow
- Requires email delivery to work
- Customer can't proceed until email verified

---

### **Layer 2: Break-Glass Admin Account**

**Automatically created with each enterprise realm:**

```csharp
// Unpredictable username
var username = $"breakglass_{tenantId}_{Guid.NewGuid():N}";

// Cryptographically secure 32-char password
var password = GenerateSecurePassword(32);

// Create in Keycloak (no email attached)
await _keycloakAdmin.CreateUserAsync(realm, username, password);

// Store in encrypted secrets manager
await _secretsManager.StoreSecretAsync($"breakglass/{tenantId}", new {
    username,
    password,
    realm,
    created = DateTime.UtcNow
});
```

**Properties:**
- Username: Unpredictable (contains GUID)
- Password: 32+ characters, random
- No email: Can't be compromised via email attacks
- Hidden: Not visible to customer
- Audited: All access logged

**Access Method:**

Platform administrators can retrieve credentials via support endpoint:
```csharp
[HttpPost("api/admin/tenants/{tenantId}/emergency-access")]
[Authorize(Roles = "PlatformAdmin")]
public async Task<IActionResult> GrantEmergencyAccess(int tenantId, EmergencyAccessDto dto)
{
    // Retrieve from secrets manager
    var secret = await _secretsManager.GetSecretAsync($"breakglass/{tenantId}");
    
    // Audit log
    _auditLogger.LogCritical($"Emergency access granted for tenant {tenantId} by {User.Identity.Name}");
    
    // Notify customer
    await _emailService.SendEmergencyAccessNotificationAsync(tenantId);
    
    // Return one-time use credentials
    return Ok(new { secret.Username, secret.Password, ExpiresIn = "15 minutes" });
}
```

**Benefits:**
- ? Always have a way to access realm
- ? Doesn't depend on customer email
- ? Secure storage (secrets manager, not database)
- ? Fully audited access

**Trade-offs:**
- Requires secrets manager infrastructure (AWS Secrets Manager, Azure Key Vault, etc.)
- Support team needs access to break-glass endpoint
- Credentials must be rotated periodically

---

### **Layer 3: Minimum Admin Enforcement**

**Prevent single point of failure:**

```csharp
// Before allowing SSO-only mode
if (await _userTenantRepo.CountAdminsAsync(tenantId) < 2)
{
    throw new InvalidOperationException(
        "Cannot disable local authentication. " +
        "Tenant must have at least 2 admin users."
    );
}
```

**UI Warning:**
```csharp
public async Task<List<string>> GetSecurityWarningsAsync(int tenantId)
{
    var warnings = new List<string>();
    var admins = await _userTenantRepo.GetAdminsAsync(tenantId);
    
    if (admins.Count == 1)
    {
        warnings.Add("CRITICAL: Only 1 admin. Invite another admin to prevent lockout.");
    }
    
    var unverifiedCount = admins.Count(a => !a.User.EmailVerified);
    if (unverifiedCount > 0)
    {
        warnings.Add($"WARNING: {unverifiedCount} admin(s) without verified email.");
    }
    
    return warnings;
}
```

**Benefits:**
- ? Prevents accidental lockout
- ? Enforces redundancy
- ? Visible warnings before problems occur

**Trade-offs:**
- Adds complexity to SSO configuration
- Requires inviting multiple users
- May delay SSO-only deployment

---

### **Layer 4: Secrets Manager Integration**

**Supported Backends:**

| Backend | Use Case | Pros | Cons |
|---------|----------|------|------|
| **AWS Secrets Manager** | AWS deployments | Native integration, automatic rotation | AWS-only, costs $ |
| **Azure Key Vault** | Azure deployments | Native integration, HSM-backed | Azure-only, costs $ |
| **HashiCorp Vault** | On-premise/hybrid | Self-hosted, multi-cloud | Requires infrastructure |
| **Environment Variables** | Development only | Simple, no dependencies | ?? INSECURE for production |

**Interface:**
```csharp
public interface ISecretsManager
{
    Task<string> StoreSecretAsync(string key, object value);
    Task<T> GetSecretAsync<T>(string key);
    Task<bool> DeleteSecretAsync(string key);
    Task RotateSecretAsync(string key);
}
```

**Example: AWS Secrets Manager**
```csharp
public class AwsSecretsManager : ISecretsManager
{
    private readonly IAmazonSecretsManager _client;
    
    public async Task<string> StoreSecretAsync(string key, object value)
    {
        var request = new CreateSecretRequest
        {
            Name = key,
            SecretString = JsonSerializer.Serialize(value),
            Tags = new List<Tag>
            {
                new Tag { Key = "Type", Value = "BreakGlass" },
                new Tag { Key = "Environment", Value = "Production" }
            }
        };
        
        var response = await _client.CreateSecretAsync(request);
        return response.ARN;
    }
    
    public async Task<T> GetSecretAsync<T>(string key)
    {
        var request = new GetSecretValueRequest { SecretId = key };
        var response = await _client.GetSecretValueAsync(request);
        return JsonSerializer.Deserialize<T>(response.SecretString);
    }
}
```

---

### **Layer 5: Audit Trail**

**All break-glass access is logged:**

```sql
CREATE TABLE AuditLog (
    Id              INT IDENTITY PRIMARY KEY,
    Timestamp       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    Action          NVARCHAR(100)   NOT NULL,
    PerformedBy     NVARCHAR(255)   NOT NULL,
    TenantId        INT             NOT NULL,
    Realm           NVARCHAR(255)   NOT NULL,
    SupportTicket   NVARCHAR(100)   NULL,
    Reason          NVARCHAR(500)   NULL,
    IpAddress       NVARCHAR(50)    NULL,
    UserAgent       NVARCHAR(500)   NULL,
    
    INDEX IX_AuditLog_TenantId (TenantId),
    INDEX IX_AuditLog_Action_Timestamp (Action, Timestamp)
);
```

**Logged Events:**
- `BREAKGLASS_ACCESS_GRANTED` - When support retrieves credentials
- `BREAKGLASS_ACCOUNT_CREATED` - When realm is provisioned
- `BREAKGLASS_PASSWORD_ROTATED` - When credentials are rotated
- `EMERGENCY_ACCESS_USED` - When break-glass account logs in

**Example Log Entry:**
```json
{
  "id": 12345,
  "timestamp": "2025-11-30T05:30:00Z",
  "action": "BREAKGLASS_ACCESS_GRANTED",
  "performedBy": "support.admin@groundup.com",
  "tenantId": 42,
  "realm": "tenant_acmelaw_a3f2",
  "supportTicket": "TICKET-9876",
  "reason": "Customer locked out - forgot password, email unverified",
  "ipAddress": "203.0.113.42",
  "userAgent": "Mozilla/5.0..."
}
```

---

## Implementation Checklist

### **Phase 1: Database & Entities** (Week 1)
- [ ] Add `RequiresEmailVerification` to `TenantInvitation`
- [ ] Create `AuditLog` entity
- [ ] Create migration for audit log table

### **Phase 2: Secrets Manager** (Week 2)
- [ ] Create `ISecretsManager` interface
- [ ] Implement AWS Secrets Manager provider
- [ ] Implement Azure Key Vault provider (optional)
- [ ] Implement development provider (env vars)
- [ ] Add configuration for secrets backend

### **Phase 3: Break-Glass Account** (Week 3)
- [ ] Implement break-glass account creation during realm provisioning
- [ ] Store credentials in secrets manager
- [ ] Create emergency access endpoint for support
- [ ] Add audit logging for all access

### **Phase 4: Email Verification** (Week 4)
- [ ] Configure Keycloak realms to require email verification
- [ ] Update invitation acceptance to check email verification
- [ ] Add email verification status to user profile
- [ ] Display verification reminder in UI

### **Phase 5: Minimum Admin Enforcement** (Week 5)
- [ ] Create `CountAdminsAsync` method in `UserTenantRepository`
- [ ] Create `GetSecurityWarningsAsync` endpoint
- [ ] Add validation before SSO-only mode
- [ ] Display warnings in admin dashboard

### **Phase 6: UI & Documentation** (Week 6)
- [ ] Security warnings component (React)
- [ ] Emergency access request form (support portal)
- [ ] Customer-facing documentation
- [ ] Support team runbook

---

## Security Considerations

### **Strengths**
- ? Multiple layers of protection
- ? Email verification prevents typos
- ? Break-glass account prevents permanent lockout
- ? Minimum admin requirement prevents single point of failure
- ? Full audit trail for compliance

### **Weaknesses**
- ?? Depends on secrets manager availability
- ?? Support team needs training on emergency access
- ?? Break-glass passwords need rotation policy
- ?? Email verification adds signup friction

### **Mitigations**
- Secrets manager should have high availability SLA
- Document emergency access procedures thoroughly
- Implement automated password rotation (90 days)
- Make email verification UX clear and helpful

---

## Customer Communication

### **During Signup**
```
?? Email sent to: julia@acme.com

Subject: Verify your email to complete Acme Law setup

Hi Julia,

Welcome to GroundUp! To complete your enterprise account setup:

1. Click this link to verify your email: [Verify Email]
2. Set your password
3. Access your Acme Law workspace

?? Important: Your email is needed for password recovery. 
Please verify it before your invitation expires (7 days).

Questions? Contact support@groundup.com
```

### **Security Best Practices Email** (After Setup)
```
Subject: Secure your Acme Law account

Hi Julia,

You've successfully set up your enterprise workspace! 

?? Security Recommendations:
1. Invite at least one more admin user
   - Prevents lockout if you lose access
   - Provides redundancy for critical operations

2. Enable two-factor authentication (coming soon)

3. Configure SSO with your company identity provider
   - Azure AD, Okta, Google Workspace supported
   - Contact support for assistance

Your current security status:
?? Only 1 admin user - invite another admin

Questions? Visit docs.groundup.com/security
```

---

## Testing Strategy

### **Unit Tests**
```csharp
[Fact]
public async Task CreateEnterpriseRealm_ShouldCreateBreakGlassAccount()
{
    // Arrange
    var dto = new CreateEnterpriseSignupDto { /* ... */ };
    
    // Act
    var result = await _tenantService.CreateEnterpriseSignupAsync(dto);
    
    // Assert
    var secret = await _secretsManager.GetSecretAsync($"breakglass/{result.TenantId}");
    Assert.NotNull(secret);
    Assert.NotEmpty(secret.Username);
    Assert.Equal(32, secret.Password.Length);
}

[Fact]
public async Task AcceptInvitation_WithUnverifiedEmail_ShouldReturnUnauthorized()
{
    // Arrange
    var invitation = new TenantInvitation { RequiresEmailVerification = true };
    var keycloakUser = new UserDetailsDto { EmailVerified = false };
    
    // Act & Assert
    await Assert.ThrowsAsync<UnauthorizedException>(
        () => _invitationService.AcceptAsync(token, keycloakUser)
    );
}
```

### **Integration Tests**
```csharp
[Fact]
public async Task BreakGlassAccount_ShouldAllowLogin()
{
    // Arrange
    var tenant = await CreateEnterpriseRealmAsync();
    var secret = await _secretsManager.GetSecretAsync($"breakglass/{tenant.Id}");
    
    // Act
    var loginResult = await LoginToKeycloakAsync(
        tenant.KeycloakRealm, 
        secret.Username, 
        secret.Password
    );
    
    // Assert
    Assert.True(loginResult.Success);
    Assert.NotNull(loginResult.AccessToken);
}
```

### **Manual Testing**
1. Create enterprise signup without verifying email ? should block login
2. Verify email ? should allow login
3. Forget password with verified email ? should receive reset link
4. Use break-glass credentials ? should log in successfully
5. Try to disable local auth with 1 admin ? should fail validation

---

## Cost Analysis

### **AWS Secrets Manager Pricing** (Example)
- **Secret storage**: $0.40/secret/month
- **API calls**: $0.05 per 10,000 calls
- **Enterprise tenant with 100 realms**:
  - Storage: 100 secrets × $0.40 = $40/month
  - API calls: ~1,000/month = $0.005
  - **Total**: ~$40/month

### **Alternatives**
- **Azure Key Vault**: $0.03/secret/month (cheaper)
- **HashiCorp Vault**: Self-hosted (infrastructure cost)
- **Database encryption**: Free but less secure

**Recommendation**: AWS Secrets Manager or Azure Key Vault for production.

---

## Rollout Plan

### **Phase 1: Development & Testing** (2 weeks)
- Implement secrets manager integration
- Create break-glass account generation
- Test emergency access flow

### **Phase 2: Staging Deployment** (1 week)
- Deploy to staging environment
- Test with real Keycloak
- Verify secrets manager integration

### **Phase 3: Production Rollout** (1 week)
- Deploy to production
- Monitor audit logs
- Create support runbook

### **Phase 4: Existing Tenant Migration** (Ongoing)
- Generate break-glass accounts for existing enterprise tenants
- Notify customers of new security features
- Offer password reset assistance

---

## Support Runbook

### **Emergency Access Request**

**Customer reports lockout:**

1. **Verify identity**
   - Request support ticket
   - Verify customer email matches invitation
   - Confirm company name and tenant ID

2. **Retrieve break-glass credentials**
   ```bash
   POST /api/admin/tenants/{tenantId}/emergency-access
   {
     "supportTicketId": "TICKET-9876",
     "reason": "Customer locked out - forgot password"
   }
   ```

3. **Provide access**
   - Share credentials via secure channel (not email)
   - Instruct customer to:
     - Log in with break-glass account
     - Reset their admin password
     - Create second admin account
     - Log out of break-glass account

4. **Follow-up**
   - Rotate break-glass password
   - Verify customer has 2+ admins
   - Document in ticket

**Average resolution time**: < 30 minutes

---

**Status**: Ready for implementation  
**Priority**: High (security critical)  
**Dependencies**: Secrets manager, email service  
**Added to**: `groundup-auth-architecture.md` Section 9
