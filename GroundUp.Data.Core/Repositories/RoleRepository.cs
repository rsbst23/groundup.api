using AutoMapper;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core.dtos;
using GroundUp.Core.entities;
using GroundUp.Core.interfaces;
using GroundUp.Data.Core.Data;

namespace GroundUp.Data.Core.Repositories;

public class RoleRepository : BaseTenantRepository<Role, RoleDto>, IRoleRepository
{
    public RoleRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
        : base(context, mapper, logger, tenantContext) { }
}
