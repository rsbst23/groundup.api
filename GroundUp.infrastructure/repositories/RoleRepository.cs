using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;

namespace GroundUp.infrastructure.repositories
{
    public class RoleRepository : BaseRepository<Role, RoleDto>, IRoleRepository
    {
        public RoleRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }
    }
}