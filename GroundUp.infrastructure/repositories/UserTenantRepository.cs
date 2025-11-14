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
    }
}
