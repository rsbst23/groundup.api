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
            if (await inventoryDb.InventoryCategories.AsNoTracking().AnyAsync())
            {
                return;
            }

            var category = new InventoryCategory
            {
                Name = "Seed Category",
                CreatedDate = DateTime.UtcNow,
                TenantId = 1
            };

            inventoryDb.InventoryCategories.Add(category);
            await inventoryDb.SaveChangesAsync();

            var item = new InventoryItem
            {
                Name = "Seed Item",
                InventoryCategoryId = category.Id,
                PurchasePrice = 9.99m,
                Condition = "New",
                PurchaseDate = DateTime.UtcNow.Date,
                TenantId = 1
            };

            inventoryDb.InventoryItems.Add(item);

            var attr = new InventoryAttribute
            {
                InventoryItem = item,
                FieldName = "color",
                FieldValue = "blue",
                TenantId = 1
            };

            inventoryDb.InventoryAttributes.Add(attr);

            await inventoryDb.SaveChangesAsync();
        }
    }
}
