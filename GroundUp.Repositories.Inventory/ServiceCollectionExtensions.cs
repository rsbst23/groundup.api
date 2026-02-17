using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Repositories.Inventory.Data;
using GroundUp.Repositories.Inventory.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Repositories.Inventory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryRepositories(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(9, 1, 0)),
                mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly("GroundUp.Repositories.Inventory");
                    mysqlOptions.EnableRetryOnFailure();
                }));

        services.AddScoped<IInventoryCategoryRepository, InventoryCategoryRepository>();
        services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();

        return services;
    }
}
