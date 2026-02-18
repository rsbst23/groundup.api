using GroundUp.Data.Core.Data;
using GroundUp.Repositories.Inventory.Data;
using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace GroundUp.Tests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<GroundUp.Sample.Program>
    {
        // Single set of database names for the entire test run
        private readonly string _coreDatabaseName = $"TestCoreDb_{Guid.NewGuid():N}";
        private readonly string _inventoryDatabaseName = $"TestInventoryDb_{Guid.NewGuid():N}";

        public CustomWebApplicationFactory()
        {
            Console.WriteLine($"[Factory] Created with databases: {_coreDatabaseName}, {_inventoryDatabaseName}");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            
            // Suppress startup noise but capture errors
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Error);
            });

            builder.ConfigureServices(services =>
            {
                try
                {
                    // Remove existing database context options
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    var inventoryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<InventoryDbContext>));
                    if (inventoryDescriptor != null)
                    {
                        services.Remove(inventoryDescriptor);
                    }

                    // Add in-memory database for testing - SHARED DATABASES for all tests
                    services.AddDbContext<ApplicationDbContext>(options => 
                    {
                        options.UseInMemoryDatabase(_coreDatabaseName);
                        options.EnableSensitiveDataLogging();
                        options.EnableDetailedErrors();
                    });
                    
                    services.AddDbContext<InventoryDbContext>(options => 
                    {
                        options.UseInMemoryDatabase(_inventoryDatabaseName);
                        options.EnableSensitiveDataLogging();
                        options.EnableDetailedErrors();
                    });

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                        options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                    }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });

                    services.AddAuthorization(options =>
                    {
                        var policy = new AuthorizationPolicyBuilder(TestAuthHandler.Scheme)
                            .RequireAuthenticatedUser()
                            .Build();

                        options.DefaultPolicy = policy;
                        options.FallbackPolicy = policy;
                    });

                    services.RemoveAll<IPermissionService>();
                    services.AddSingleton<IPermissionService, TestPermissionService>();

                    Console.WriteLine($"[Factory] Services configured successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Factory] ERROR during ConfigureServices: {ex.Message}");
                    Console.WriteLine($"[Factory] Stack: {ex.StackTrace}");
                    throw;
                }
            });

            // Initialize databases after services are configured
            builder.ConfigureServices((context, services) =>
            {
                try
                {
                    var sp = services.BuildServiceProvider();
                    
                    using var scope = sp.CreateScope();
                    var scopedServices = scope.ServiceProvider;

                    var coreDb = scopedServices.GetRequiredService<ApplicationDbContext>();
                    coreDb.Database.EnsureCreated();
                    Console.WriteLine($"[Factory] Core database initialized");

                    var inventoryDb = scopedServices.GetRequiredService<InventoryDbContext>();
                    inventoryDb.Database.EnsureCreated();
                    Console.WriteLine($"[Factory] Inventory database initialized");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Factory] ERROR during database initialization: {ex.Message}");
                    Console.WriteLine($"[Factory] Stack: {ex.StackTrace}");
                    throw;
                }
            });
        }
    }
}
