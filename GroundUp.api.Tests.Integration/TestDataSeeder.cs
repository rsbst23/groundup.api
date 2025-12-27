using GroundUp.core.entities;
using GroundUp.infrastructure.data;

namespace GroundUp.Tests.Integration
{
    public static class TestDataSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext dbContext)
        {
            if (!dbContext.InventoryCategories.Any())
            {
                await dbContext.InventoryCategories.AddRangeAsync(
                    new InventoryCategory { Id = 1, Name = "Electronics", TenantId = TestData.TenantId },
                    new InventoryCategory { Id = 2, Name = "Books", TenantId = TestData.TenantId }
                );
            }

            if (!dbContext.InventoryItems.Any())
            {
                await dbContext.InventoryItems.AddRangeAsync(
                    new InventoryItem { Id = 1, Name = "Laptop", PurchasePrice = 999.99m, Condition = "New", InventoryCategoryId = 1, PurchaseDate = DateTime.UtcNow, TenantId = TestData.TenantId },
                    new InventoryItem { Id = 2, Name = "The Great Gatsby", PurchasePrice = 12.99m, Condition = "Used", InventoryCategoryId = 2, PurchaseDate = DateTime.UtcNow, TenantId = TestData.TenantId }
                );
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
