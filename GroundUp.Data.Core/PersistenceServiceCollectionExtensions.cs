using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Data.Core.Data;
using GroundUp.Data.Core.Mapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Data.Core;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Core EF Core persistence layer (DbContext, UnitOfWork, and Core repositories).
    /// This keeps the host app from referencing <see cref="ApplicationDbContext"/> directly.
    /// </summary>
    public static IServiceCollection AddCorePersistence(this IServiceCollection services, string connectionString)
    {
        // AutoMapper mappings for EF entities <-> DTOs used in repositories.
        services.AddAutoMapper(typeof(CoreMappingProfile).Assembly);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0)),
                mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly("GroundUp.Data.Core");
                    mysqlOptions.EnableRetryOnFailure();
                }));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddCoreRepositories();

        return services;
    }
}
