using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;

namespace GroundUp.infrastructure.repositories
{
    public class InventoryItemRepository : BaseTenantRepository<InventoryItem, InventoryItemDto>, IInventoryItemRepository
    {
        public InventoryItemRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
            : base(context, mapper, logger, tenantContext) { }

        // Any additional entity-specific methods can go here
    }
}
