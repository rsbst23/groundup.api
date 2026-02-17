using AutoMapper;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.Data.Core.Data;

namespace GroundUp.Data.Core.Repositories;

public class PermissionRepository : BaseRepository<Permission, PermissionDto>, IPermissionRepository
{
    public PermissionRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
        : base(context, mapper, logger) { }
}
