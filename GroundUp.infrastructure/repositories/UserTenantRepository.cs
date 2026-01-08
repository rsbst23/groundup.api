using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.Repositories.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GroundUp.infrastructure.repositories
{
    public class UserTenantRepository : BaseTenantRepository<UserTenant, UserTenantDto>, IUserTenantRepository
    {
        public UserTenantRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
            : base(context, mapper, logger, tenantContext) { }

        public async Task<List<UserTenantDto>> GetTenantsForUserAsync(Guid userId)
        {
            var userTenants = await _context.Set<UserTenant>()
                .Include(ut => ut.Tenant)
                .Where(ut => ut.UserId == userId)
                .ToListAsync();
            return _mapper.Map<List<UserTenantDto>>(userTenants);
        }

        public async Task<UserTenantDto?> GetUserTenantAsync(Guid userId, int tenantId)
        {
            var userTenant = await _context.Set<UserTenant>()
                .Include(ut => ut.Tenant)
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);
            return userTenant == null ? null : _mapper.Map<UserTenantDto>(userTenant);
        }

        public Task<bool> TenantHasAnyMembersAsync(int tenantId)
        {
            return _context.Set<UserTenant>()
                .AsNoTracking()
                .AnyAsync(ut => ut.TenantId == tenantId);
        }

        public async Task<UserTenantDto?> GetByRealmAndExternalUserIdAsync(string realmName, string externalUserId)
        {
            _logger.LogInformation($"Looking up UserTenant: Realm='{realmName}', ExternalUserId='{externalUserId}'");

            // First try exact match
            var userTenant = await _context.Set<UserTenant>()
                .Include(ut => ut.Tenant)
                .Include(ut => ut.User)
                .FirstOrDefaultAsync(ut =>
                    ut.Tenant.RealmName == realmName &&
                    ut.ExternalUserId == externalUserId);

            if (userTenant != null)
            {
                _logger.LogInformation($"Found UserTenant: UserId={userTenant.UserId}, TenantId={userTenant.TenantId}, TenantRealm={userTenant.Tenant?.RealmName}");
                return _mapper.Map<UserTenantDto>(userTenant);
            }

            // If not found, log all matching records by ExternalUserId for debugging
            var allMatches = await _context.Set<UserTenant>()
                .Include(ut => ut.Tenant)
                .Include(ut => ut.User)
                .Where(ut => ut.ExternalUserId == externalUserId)
                .ToListAsync();

            if (allMatches.Any())
            {
                _logger.LogWarning($"No match for realm '{realmName}', but found {allMatches.Count} UserTenant(s) with ExternalUserId '{externalUserId}':");
                foreach (var match in allMatches)
                {
                    _logger.LogWarning($"  - UserId={match.UserId}, TenantId={match.TenantId}, TenantRealm='{match.Tenant?.RealmName}'");
                }

                // TEMPORARY: Return first match regardless of realm (for debugging)
                _logger.LogWarning($"TEMPORARY FIX: Returning first match regardless of realm");
                return _mapper.Map<UserTenantDto>(allMatches[0]);
            }

            _logger.LogWarning($"No UserTenant found for ExternalUserId '{externalUserId}' in any realm");
            return null;
        }

        public async Task<UserTenantDto> AssignUserToTenantAsync(Guid userId, int tenantId, bool isAdmin = false, string? externalUserId = null)
        {
            var userTenants = _context.Set<UserTenant>();

            // Check if mapping already exists
            var existing = await userTenants
                .Include(ut => ut.Tenant)
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);

            if (existing != null)
            {
                // Update fields if different
                bool updated = false;
                if (existing.IsAdmin != isAdmin)
                {
                    existing.IsAdmin = isAdmin;
                    updated = true;
                }
                if (!string.IsNullOrEmpty(externalUserId) && existing.ExternalUserId != externalUserId)
                {
                    existing.ExternalUserId = externalUserId;
                    updated = true;
                }

                if (updated)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Updated UserTenant for user {userId} in tenant {tenantId}");
                }
                else
                {
                    _logger.LogInformation($"User {userId} is already assigned to tenant {tenantId}");
                }
                return _mapper.Map<UserTenantDto>(existing);
            }

            // Create new mapping
            var userTenant = new UserTenant
            {
                UserId = userId,
                TenantId = tenantId,
                IsAdmin = isAdmin,
                ExternalUserId = externalUserId,
                JoinedAt = DateTime.UtcNow
            };

            userTenants.Add(userTenant);
            await _context.SaveChangesAsync();

            // Reload with Tenant navigation property
            var created = await userTenants
                .Include(ut => ut.Tenant)
                .FirstAsync(ut => ut.Id == userTenant.Id);

            _logger.LogInformation($"Successfully assigned user {userId} to tenant {tenantId} (IsAdmin: {isAdmin}, ExternalUserId: {externalUserId})");
            return _mapper.Map<UserTenantDto>(created);
        }

        public async Task<bool> RemoveUserFromTenantAsync(Guid userId, int tenantId)
        {
            var userTenants = _context.Set<UserTenant>();

            var userTenant = await userTenants
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);

            if (userTenant == null)
            {
                _logger.LogWarning($"UserTenant mapping not found for user {userId} and tenant {tenantId}");
                return false;
            }

            userTenants.Remove(userTenant);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully removed user {userId} from tenant {tenantId}");
            return true;
        }
    }
}
