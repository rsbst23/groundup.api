using GroundUp.Repositories.Inventory.Data;
using GroundUp.Repositories.Inventory.Entities;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.Tests.Integration
{
    internal static class TestDataSeeder
    {
        public static async Task SeedInventoryAsync(InventoryDbContext inventoryDb)
        {
            // Idempotent seed to keep tests stable.
            if (!inventoryDb.InventoryCategories.Any())
            {
                await inventoryDb.InventoryCategories.AddRangeAsync(
                    new InventoryCategory { Id = 1, Name = "Electronics", CreatedDate = DateTime.UtcNow, TenantId = 1 },
                    new InventoryCategory { Id = 2, Name = "Books", CreatedDate = DateTime.UtcNow, TenantId = 1 }
                );
            }

            if (!inventoryDb.InventoryItems.Any())
            {
                await inventoryDb.InventoryItems.AddRangeAsync(
                    new InventoryItem { Id = 1, Name = "Laptop", PurchasePrice = 999.99m, Condition = "New", InventoryCategoryId = 1, PurchaseDate = DateTime.UtcNow, TenantId = 1 },
                    new InventoryItem { Id = 2, Name = "The Great Gatsby", PurchasePrice = 12.99m, Condition = "Used", InventoryCategoryId = 2, PurchaseDate = DateTime.UtcNow, TenantId = 1 }
                );
            }

            await inventoryDb.SaveChangesAsync();
        }
    }
}
