using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;

namespace GroundUp.infrastructure.repositories
{
    public class InventoryItemRepository : BaseRepository<InventoryItem, InventoryItemDto>, IInventoryItemRepository
    {
        public InventoryItemRepository(ApplicationDbContext context, IMapper mapper)
            : base(context, mapper) { }

        // Any additional entity-specific methods can go here
    }
}
