using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class RolePolicyRepository : BaseRepository<RolePolicy, RolePolicyDto>, IRolePolicyRepository
    {
        public RolePolicyRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }
    }
}