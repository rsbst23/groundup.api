using AutoMapper;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;
using GroundUp.Repositories.Inventory.Data;
using GroundUp.Repositories.Inventory.Entities;
using GroundUp.Repositories.Inventory.Repositories.Base;

namespace GroundUp.Repositories.Inventory.Repositories;

public sealed class InventoryItemRepository
    : BaseTenantRepository<InventoryItem, InventoryItemDto>, IInventoryItemRepository
{
    public InventoryItemRepository(
        InventoryDbContext context,
        IMapper mapper,
        ILoggingService logger,
        ITenantContext tenantContext)
        : base(context, mapper, logger, tenantContext)
    {
    }
}
