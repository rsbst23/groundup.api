using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;

namespace GroundUp.infrastructure.repositories
{
    public class PermissionRepository : BaseRepository<Permission, PermissionDto>, IPermissionRepository
    {
        public PermissionRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }
    }
}