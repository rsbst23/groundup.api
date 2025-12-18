# Background Cleanup Job Design

## Overview

This document specifies the background job system for cleaning up orphaned Keycloak identities, unused enterprise tenants, and abandoned realms.

---

## 1. Orphaned Keycloak Identity Cleanup

### **What is an Orphaned Identity?**

A `UserKeycloakIdentity` becomes orphaned when:
- The Keycloak user is deleted (via Keycloak Admin UI)
- The Keycloak realm is deleted
- The user exists in GroundUp database but not in Keycloak

### **Detection Strategy**

```csharp
// For each UserKeycloakIdentity:
1. Call Keycloak Admin API: GET /admin/realms/{realm}/users/{keycloakUserId}
2. If 404 Not Found ? identity is orphaned
3. If realm doesn't exist ? identity is orphaned
4. If user exists ? identity is valid
```

### **Cleanup Actions**

**Option A: Soft Delete (Recommended)**
```sql
ALTER TABLE UserKeycloakIdentities 
ADD IsActive BIT NOT NULL DEFAULT 1,
ADD DeletedAt DATETIME(6) NULL;

-- Mark as inactive instead of deleting
UPDATE UserKeycloakIdentities 
SET IsActive = 0, DeletedAt = GETUTCDATE()
WHERE (RealmName, KeycloakUserId) = orphaned identity
```

**Why soft delete?**
- Audit trail for security investigations
- Can restore if Keycloak user was temporarily disabled
- Historical data for analytics

**Option B: Hard Delete (After Grace Period)**
```csharp
// Delete identities orphaned for > 30 days
DELETE FROM UserKeycloakIdentities 
WHERE IsActive = 0 
  AND DeletedAt < DATEADD(day, -30, GETUTCDATE());
```

### **User Cleanup After Identity Removal**

```csharp
// After marking identity as inactive:
var user = await _userRepository.GetByIdAsync(identity.UserId);

// Check if user has any other active identities
var activeIdentities = await _identityRepo.GetActiveIdentitiesForUserAsync(user.Id);

if (activeIdentities.Count == 0)
{
    // Check if user has any tenant memberships
    var tenantMemberships = await _userTenantRepo.GetTenantsForUserAsync(user.Id);
    
    if (tenantMemberships.Count == 0)
    {
        // User has no identities and no tenant access
        // Mark user as inactive (soft delete)
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        
        _logger.LogWarning($"User {user.Id} marked inactive - no Keycloak identities or tenant access");
    }
    else
    {
        // User still has tenant access via other identities
        // Keep user active but log for investigation
        _logger.LogWarning($"User {user.Id} has orphaned identity but still has tenant access");
    }
}
```

---

## 2. Unused Enterprise Tenant Cleanup

### **What is an Unused Tenant?**

An enterprise tenant is considered unused if:
- Created > 30 days ago
- First admin invitation never accepted
- No users assigned (`UserTenants` count = 0)
- Realm exists in Keycloak but has 0 users

### **Detection Query**

```sql
SELECT t.Id, t.Name, t.KeycloakRealm, t.CreatedAt
FROM Tenants t
WHERE t.TenantType = 'enterprise'
  AND t.CreatedAt < DATEADD(day, -30, GETUTCDATE())
  AND NOT EXISTS (
      SELECT 1 FROM UserTenants ut WHERE ut.TenantId = t.Id
  )
  AND EXISTS (
      SELECT 1 FROM TenantInvitations ti 
      WHERE ti.TenantId = t.Id 
        AND ti.IsAdmin = 1 
        AND ti.IsAccepted = 0
  );
```

### **Cleanup Actions**

**Step 1: Notification (15 days before deletion)**
```csharp
// Send warning email to ContactEmail on invitation
var invitation = await _invitationRepo.GetFirstAdminInvitationAsync(tenant.Id);

if (!string.IsNullOrEmpty(invitation.ContactEmail))
{
    await _emailService.SendAsync(new
    {
        To = invitation.ContactEmail,
        Subject = "Enterprise Tenant Pending Deletion",
        Body = $"Your enterprise tenant '{tenant.Name}' will be deleted in 15 days due to inactivity. " +
               $"Accept your invitation to prevent deletion: {invitationUrl}"
    });
}
```

**Step 2: Delete Realm from Keycloak**
```csharp
await _keycloakAdmin.DeleteRealmAsync(tenant.KeycloakRealm);
```

**Step 3: Soft Delete Tenant**
```sql
UPDATE Tenants 
SET IsActive = 0, UpdatedAt = GETUTCDATE()
WHERE Id = @tenantId;

-- Also mark invitations as expired
UPDATE TenantInvitations 
SET ExpiresAt = GETUTCDATE()
WHERE TenantId = @tenantId AND IsAccepted = 0;
```

---

## 3. Background Job Implementation

### **Technology Choice**

**Option A: Hangfire (Recommended)**
```bash
dotnet add package Hangfire.Core
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.MySqlStorage
```

**Why Hangfire?**
- Built-in dashboard (monitoring)
- Persistent storage (survives restarts)
- Cron scheduling
- Retry logic
- No external dependencies (Redis, RabbitMQ)

**Option B: Built-in HostedService**
```csharp
public class CleanupBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupOrphanedIdentitiesAsync();
            await CleanupUnusedTenantsAsync();
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

**Why NOT HostedService?**
- No persistence (lost on restart)
- No dashboard
- Manual retry logic
- Single instance only (can't scale)

### **Hangfire Implementation**

**Program.cs**
```csharp
// Add Hangfire services
builder.Services.AddHangfire(config => config
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseStorage(new MySqlStorage(
        connectionString,
        new MySqlStorageOptions
        {
            TablesPrefix = "Hangfire_"
        }
    )));

builder.Services.AddHangfireServer();

// Register cleanup service
builder.Services.AddScoped<ICleanupService, CleanupService>();

var app = builder.Build();

// Add Hangfire dashboard (secured)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Schedule recurring jobs
RecurringJob.AddOrUpdate<ICleanupService>(
    "cleanup-orphaned-identities",
    service => service.CleanupOrphanedIdentitiesAsync(),
    Cron.Daily(2)); // Run at 2 AM daily

RecurringJob.AddOrUpdate<ICleanupService>(
    "cleanup-unused-tenants",
    service => service.CleanupUnusedTenantsAsync(),
    Cron.Weekly(DayOfWeek.Sunday, 3)); // Run Sundays at 3 AM
```

**ICleanupService.cs**
```csharp
namespace GroundUp.core.interfaces
{
    public interface ICleanupService
    {
        /// <summary>
        /// Scans UserKeycloakIdentities and marks orphaned identities as inactive
        /// </summary>
        Task CleanupOrphanedIdentitiesAsync();

        /// <summary>
        /// Deletes unused enterprise tenants and their Keycloak realms
        /// </summary>
        Task CleanupUnusedTenantsAsync();

        /// <summary>
        /// Sends warning emails for tenants pending deletion
        /// </summary>
        Task SendTenantDeletionWarningsAsync();
    }
}
```

**CleanupService.cs**
```csharp
namespace GroundUp.infrastructure.services
{
    public class CleanupService : ICleanupService
    {
        private readonly IUserKeycloakIdentityRepository _identityRepo;
        private readonly ITenantRepository _tenantRepo;
        private readonly IIdentityProviderAdminService _keycloakAdmin;
        private readonly ILoggingService _logger;
        private readonly IEmailService _emailService;

        public async Task CleanupOrphanedIdentitiesAsync()
        {
            _logger.LogInformation("Starting orphaned identity cleanup");
            
            var allIdentities = await _identityRepo.GetAllActiveIdentitiesAsync();
            var orphanedCount = 0;
            var errorCount = 0;

            foreach (var identity in allIdentities)
            {
                try
                {
                    // Check if Keycloak user still exists
                    var keycloakUser = await _keycloakAdmin.GetUserByIdAsync(
                        identity.KeycloakUserId, 
                        identity.RealmName);

                    if (keycloakUser == null)
                    {
                        // User doesn't exist in Keycloak - mark as orphaned
                        await _identityRepo.MarkAsInactiveAsync(identity.Id);
                        orphanedCount++;
                        
                        _logger.LogWarning(
                            $"Marked identity as orphaned: " +
                            $"UserId={identity.UserId}, " +
                            $"Realm={identity.RealmName}, " +
                            $"KeycloakUserId={identity.KeycloakUserId}");

                        // Check if user should be deactivated
                        await CheckAndDeactivateUserAsync(identity.UserId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"Error checking identity {identity.Id}: {ex.Message}", ex);
                    errorCount++;
                }
            }

            _logger.LogInformation(
                $"Orphaned identity cleanup complete: " +
                $"{orphanedCount} marked inactive, {errorCount} errors");
        }

        public async Task CleanupUnusedTenantsAsync()
        {
            _logger.LogInformation("Starting unused tenant cleanup");

            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var unusedTenants = await _tenantRepo.GetUnusedEnterpriseTenantsAsync(cutoffDate);
            var deletedCount = 0;

            foreach (var tenant in unusedTenants)
            {
                try
                {
                    _logger.LogWarning($"Deleting unused tenant: {tenant.Name} (ID: {tenant.Id})");

                    // Delete Keycloak realm
                    await _keycloakAdmin.DeleteRealmAsync(tenant.KeycloakRealm);

                    // Soft delete tenant
                    await _tenantRepo.SoftDeleteAsync(tenant.Id);

                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"Error deleting tenant {tenant.Id}: {ex.Message}", ex);
                }
            }

            _logger.LogInformation($"Deleted {deletedCount} unused enterprise tenants");
        }

        public async Task SendTenantDeletionWarningsAsync()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-15); // Warn at 15 days
            var warningTenants = await _tenantRepo.GetUnusedEnterpriseTenantsAsync(cutoffDate);

            foreach (var tenant in warningTenants)
            {
                var invitation = await _invitationRepo.GetFirstAdminInvitationAsync(tenant.Id);
                
                if (!string.IsNullOrEmpty(invitation?.ContactEmail))
                {
                    await _emailService.SendTenantDeletionWarningAsync(
                        invitation.ContactEmail,
                        tenant.Name,
                        invitation.InvitationToken);
                }
            }
        }

        private async Task CheckAndDeactivateUserAsync(Guid userId)
        {
            var activeIdentities = await _identityRepo.GetActiveIdentitiesForUserAsync(userId);
            
            if (activeIdentities.Count == 0)
            {
                var tenants = await _userTenantRepo.GetTenantsForUserAsync(userId);
                
                if (tenants.Count == 0)
                {
                    await _userRepository.SoftDeleteAsync(userId);
                    _logger.LogWarning($"User {userId} deactivated - no identities or tenant access");
                }
            }
        }
    }
}
```

---

## 4. Database Schema Updates

**Add IsActive column to UserKeycloakIdentities**
```sql
ALTER TABLE UserKeycloakIdentities
ADD IsActive BIT NOT NULL DEFAULT 1,
ADD DeletedAt DATETIME(6) NULL;

CREATE INDEX IX_UserKeycloakIdentities_IsActive 
ON UserKeycloakIdentities(IsActive);
```

**Add soft delete to Users**
```sql
-- Already exists: IsActive column
CREATE INDEX IX_Users_IsActive 
ON Users(IsActive);
```

---

## 5. Monitoring & Alerts

### **Hangfire Dashboard**

Access at: `https://app.localhost/hangfire`

Shows:
- Job execution history
- Failed jobs
- Retry attempts
- Job duration metrics

### **Logging**

All cleanup operations log:
- Start/end time
- Count of items processed
- Count of items deleted
- Errors encountered

### **Alerts**

Trigger alerts when:
- More than 100 orphaned identities in single run
- Cleanup job fails 3 times in a row
- Realm deletion fails

---

## 6. Configuration

**appsettings.json**
```json
{
  "CleanupJob": {
    "OrphanedIdentityGracePeriodDays": 0,
    "UnusedTenantGracePeriodDays": 30,
    "DeletionWarningDays": 15,
    "BatchSize": 100,
    "EnableAutoCleanup": true
  }
}
```

---

## 7. Testing Strategy

### **Unit Tests**
- Mock Keycloak responses (404, 200)
- Verify soft delete logic
- Verify user deactivation logic

### **Integration Tests**
- Create Keycloak user, delete it, verify cleanup
- Create unused tenant, verify deletion after 30 days

### **Manual Testing**
- Use Hangfire dashboard to trigger jobs manually
- Verify email warnings sent
- Verify realm deletion in Keycloak

---

## 8. Rollout Plan

**Phase 1: Monitoring Only**
- Deploy cleanup service
- Run jobs but DON'T delete anything
- Just log what WOULD be deleted
- Review logs for 2 weeks

**Phase 2: Soft Deletes**
- Enable soft deletes (mark IsActive = 0)
- Don't hard delete yet
- Monitor for false positives

**Phase 3: Full Cleanup**
- Enable hard deletes after grace period
- Enable realm deletion
- Monitor closely for first month

---

## 9. Operational Procedures

### **Manual Cleanup Trigger**

If immediate cleanup needed:
```bash
# Via Hangfire dashboard
https://app.localhost/hangfire
-> Recurring Jobs
-> "cleanup-orphaned-identities"
-> Trigger Now

# Or via API
POST /api/admin/cleanup/orphaned-identities
POST /api/admin/cleanup/unused-tenants
```

### **Restore Orphaned Identity**

If identity was marked inactive by mistake:
```sql
UPDATE UserKeycloakIdentities
SET IsActive = 1, DeletedAt = NULL
WHERE RealmName = 'groundup' AND KeycloakUserId = 'abc123';
```

### **Prevent Tenant Deletion**

If tenant should not be deleted:
```sql
-- Manually create a user tenant assignment
INSERT INTO UserTenants (UserId, TenantId, IsAdmin, JoinedAt)
VALUES (@existingUserId, @tenantId, 1, GETUTCDATE());
```

---

## 10. Security Considerations

- Hangfire dashboard requires authentication (system admin only)
- Cleanup jobs run with elevated permissions
- All deletions logged with audit trail
- Grace periods prevent accidental deletions
- Email warnings give users chance to prevent deletion

---

**Status**: Ready for implementation  
**Priority**: Medium (implement after Phase 2 auth updates)  
**Dependencies**: Hangfire package, email service
