using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.Repositories.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class PermissionRepository : BaseRepository<Permission, PermissionDto>, IPermissionRepository
    {
        public PermissionRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }
    }
}