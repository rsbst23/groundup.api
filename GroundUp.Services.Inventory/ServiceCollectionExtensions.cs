using Castle.DynamicProxy;
using GroundUp.Services.Core.Interceptors;
using GroundUp.Services.Inventory.Interfaces;
using GroundUp.Services.Inventory.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Services.Inventory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryServices(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddProfile<InventoryMappingProfile>());

        // NOTE: The composition root (e.g., `GroundUp.Sample`) is expected to call
        // `services.AddInfrastructureServices()` once. We only rely on the proxy types
        // registered there (e.g., `ProxyGenerator`, `PermissionInterceptor`).

        services.AddProxiedScoped<IInventoryCategoryService, InventoryCategoryService>();
        services.AddProxiedScoped<IInventoryItemService, InventoryItemService>();

        return services;
    }

    private static IServiceCollection AddProxiedScoped<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddScoped<TImplementation>();
        services.AddScoped<TInterface>(sp =>
        {
            var generator = sp.GetRequiredService<ProxyGenerator>();
            var impl = sp.GetRequiredService<TImplementation>();
            var interceptor = sp.GetRequiredService<PermissionInterceptor>();
            return generator.CreateInterfaceProxyWithTarget<TInterface>(impl, interceptor);
        });

        return services;
    }
}
