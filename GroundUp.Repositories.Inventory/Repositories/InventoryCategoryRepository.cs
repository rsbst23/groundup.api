using AutoMapper;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.Repositories.Inventory.Data;
using GroundUp.Repositories.Inventory.Entities;
using GroundUp.Repositories.Inventory.Repositories.Base;

namespace GroundUp.Repositories.Inventory.Repositories;

public sealed class InventoryCategoryRepository
    : BaseTenantRepository<InventoryCategory, InventoryCategoryDto>, IInventoryCategoryRepository
{
    public InventoryCategoryRepository(
        InventoryDbContext context,
        IMapper mapper,
        ILoggingService logger,
        ITenantContext tenantContext)
        : base(context, mapper, logger, tenantContext)
    {
    }
}
