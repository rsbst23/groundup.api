using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;

namespace GroundUp.infrastructure.repositories
{
    public class InventoryCategoryRepository : BaseTenantRepository<InventoryCategory, InventoryCategoryDto>, IInventoryCategoryRepository
    {
        public InventoryCategoryRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
            : base(context, mapper, logger, tenantContext) { }
    }
}
