using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;

namespace GroundUp.infrastructure.repositories
{
    public class RolePolicyRepository : BaseTenantRepository<RolePolicy, RolePolicyDto>, IRolePolicyRepository
    {
        public RolePolicyRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
            : base(context, mapper, logger, tenantContext) { }
    }
}