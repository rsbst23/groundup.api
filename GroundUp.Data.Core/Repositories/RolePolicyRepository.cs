using AutoMapper;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.Data.Core.Data;

namespace GroundUp.Data.Core.Repositories;

public class RolePolicyRepository : BaseTenantRepository<RolePolicy, RolePolicyDto>, IRolePolicyRepository
{
    public RolePolicyRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
        : base(context, mapper, logger, tenantContext) { }
}
