using System.Net.Http;
using Xunit;
using GroundUp.infrastructure.data;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace GroundUp.Tests.Integration
{
    [Collection("DatabaseTests")] // Ensures tests run sequentially
    public abstract class BaseIntegrationTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        protected readonly HttpClient _client;
        protected readonly IServiceScope _scope;
        protected readonly ApplicationDbContext _dbContext;

        protected BaseIntegrationTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        }

        // Clears and seeds the database before each test
        public async Task InitializeAsync()
        {
            await ResetDatabase();
            await TestDataSeeder.SeedAsync(_dbContext);
        }

        // Cleanup if needed (optional for in-memory DB)
        public Task DisposeAsync() => Task.CompletedTask;

        // Clear the database before each test
        private async Task ResetDatabase()
        {
            _dbContext.InventoryItems.RemoveRange(_dbContext.InventoryItems);
            _dbContext.InventoryCategories.RemoveRange(_dbContext.InventoryCategories);
            await _dbContext.SaveChangesAsync();
        }
    }
}
