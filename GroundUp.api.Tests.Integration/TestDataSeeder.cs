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
                    new InventoryCategory { Id = 1, Name = "Electronics" },
                    new InventoryCategory { Id = 2, Name = "Books" }
                );
            }

            if (!dbContext.InventoryItems.Any())
            {
                await dbContext.InventoryItems.AddRangeAsync(
                    new InventoryItem { Id = 1, Name = "Laptop", PurchasePrice = 999.99m, Condition = "New", InventoryCategoryId = 1, PurchaseDate = DateTime.UtcNow },
                    new InventoryItem { Id = 2, Name = "The Great Gatsby", PurchasePrice = 12.99m, Condition = "Used", InventoryCategoryId = 2, PurchaseDate = DateTime.UtcNow }
                );
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
