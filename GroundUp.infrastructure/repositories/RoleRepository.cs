using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.Repositories.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class RoleRepository : BaseTenantRepository<Role, RoleDto>, IRoleRepository
    {
        public RoleRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
            : base(context, mapper, logger, tenantContext) { }
    }
}