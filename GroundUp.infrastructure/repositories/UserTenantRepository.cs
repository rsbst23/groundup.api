using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
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
            var userTenants = await _context.UserTenants
                .Include(ut => ut.Tenant)
                .Where(ut => ut.UserId == userId)
                .ToListAsync();
            return _mapper.Map<List<UserTenantDto>>(userTenants);
        }

        public async Task<UserTenantDto?> GetUserTenantAsync(Guid userId, int tenantId)
        {
            var userTenant = await _context.UserTenants
                .Include(ut => ut.Tenant)
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);
            return userTenant == null ? null : _mapper.Map<UserTenantDto>(userTenant);
        }

        public async Task<UserTenantDto> AssignUserToTenantAsync(Guid userId, int tenantId)
        {
            // Check if mapping already exists
            var existing = await _context.UserTenants
                .Include(ut => ut.Tenant)
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);

            if (existing != null)
            {
                _logger.LogInformation($"User {userId} is already assigned to tenant {tenantId}");
                return _mapper.Map<UserTenantDto>(existing);
            }

            // Create new mapping
            var userTenant = new UserTenant
            {
                UserId = userId,
                TenantId = tenantId
            };

            _context.UserTenants.Add(userTenant);
            await _context.SaveChangesAsync();

            // Reload with Tenant navigation property
            var created = await _context.UserTenants
                .Include(ut => ut.Tenant)
                .FirstAsync(ut => ut.Id == userTenant.Id);

            _logger.LogInformation($"Successfully assigned user {userId} to tenant {tenantId}");
            return _mapper.Map<UserTenantDto>(created);
        }

        public async Task<bool> RemoveUserFromTenantAsync(Guid userId, int tenantId)
        {
            var userTenant = await _context.UserTenants
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);

            if (userTenant == null)
            {
                _logger.LogWarning($"UserTenant mapping not found for user {userId} and tenant {tenantId}");
                return false;
            }

            _context.UserTenants.Remove(userTenant);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully removed user {userId} from tenant {tenantId}");
            return true;
        }
    }
}
