using System.Net.Http;
using Xunit;
using GroundUp.Data.Core.Data;
using GroundUp.Repositories.Inventory.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections;
using System.Reflection;

namespace GroundUp.Tests.Integration
{
    [Collection("DatabaseTests")] // Ensures tests run sequentially within this collection
    public abstract class BaseIntegrationTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        protected readonly HttpClient _client;
        protected readonly IServiceScope _scope;
        protected readonly ApplicationDbContext _coreDbContext;
        protected readonly InventoryDbContext _inventoryDbContext;

        protected BaseIntegrationTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _coreDbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _inventoryDbContext = _scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        }

        public async Task InitializeAsync()
        {
            await ResetDatabaseAsync(_inventoryDbContext);
            await ResetDatabaseAsync(_coreDbContext);

            await TestDataSeeder.SeedInventoryAsync(_inventoryDbContext);
        }

        public Task DisposeAsync()
        {
            _scope.Dispose();
            _client.Dispose();
            return Task.CompletedTask;
        }

        private static async Task ResetDatabaseAsync(DbContext dbContext)
        {
            dbContext.ChangeTracker.Clear();

            var orderedEntityTypes = dbContext.Model.GetEntityTypes()
                .Where(et => !et.IsOwned() && et.FindPrimaryKey() != null)
                .OrderByDescending(et => et.GetForeignKeys().Count())
                .Select(et => et.ClrType)
                .Distinct()
                .ToList();

            var setMethod = typeof(DbContext).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

            foreach (var clrType in orderedEntityTypes)
            {
                var genericSetMethod = setMethod.MakeGenericMethod(clrType);
                var setObj = genericSetMethod.Invoke(dbContext, null);

                if (setObj is not IQueryable queryable)
                {
                    continue;
                }

                var entities = queryable.Cast<object>().ToList();
                if (entities.Count == 0)
                {
                    continue;
                }

                dbContext.RemoveRange(entities);
            }

            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
        }
    }
}
