using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using GroundUp.infrastructure.data;

namespace GroundUp.infrastructure.repositories
{
    public class InventoryCategoryRepository : BaseRepository<InventoryCategory, InventoryCategoryDto>, IInventoryCategoryRepository
    {
        public InventoryCategoryRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger) : base(context, mapper, logger)
        {
        }        
    }
}
