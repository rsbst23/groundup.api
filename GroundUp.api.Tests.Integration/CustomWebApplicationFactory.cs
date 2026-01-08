using GroundUp.Repositories.Core.Data;
using GroundUp.Repositories.Inventory.Data;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GroundUp.Tests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"TestDb_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing database context
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Remove existing inventory db context (if registered by Program.cs)
                var inventoryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<InventoryDbContext>));
                if (inventoryDescriptor != null)
                {
                    services.Remove(inventoryDescriptor);
                }

                // Add in-memory database for testing (unique per factory instance)
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                });

                services.AddDbContext<InventoryDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                });

                // Replace authentication with a test scheme
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });

                // Replace authorization to use the test scheme for default/fallback
                services.AddAuthorization(options =>
                {
                    var policy = new AuthorizationPolicyBuilder(TestAuthHandler.Scheme)
                        .RequireAuthenticatedUser()
                        .Build();

                    options.DefaultPolicy = policy;
                    options.FallbackPolicy = policy;
                });

                // Replace permission service so RequiresPermission checks don't block tests
                services.RemoveAll<IPermissionService>();
                services.AddSingleton<IPermissionService, TestPermissionService>();

                // Ensure databases are created
                using var scope = services.BuildServiceProvider().CreateScope();
                var coreDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                coreDb.Database.EnsureCreated();

                var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                inventoryDb.Database.EnsureCreated();
            });
        }
    }
}
