using GroundUp.core.interfaces;
using GroundUp.Services.Inventory.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Services.Inventory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryServices(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddProfile<InventoryMappingProfile>());

        services.AddScoped<IInventoryCategoryService, InventoryCategoryService>();
        services.AddScoped<IInventoryItemService, InventoryItemService>();
        return services;
    }
}
