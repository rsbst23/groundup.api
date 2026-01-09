using GroundUp.Repositories.Inventory.Entities;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.Repositories.Inventory.Data
{
    public sealed class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

        public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<InventoryAttribute> InventoryAttributes => Set<InventoryAttribute>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<InventoryCategory>()
                .Property(b => b.CreatedDate)
                .HasColumnType("DATETIME(6)");

            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.InventoryCategory)
                .WithMany(c => c.InventoryItems)
                .HasForeignKey(i => i.InventoryCategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InventoryAttribute>()
                .HasOne(a => a.InventoryItem)
                .WithMany(i => i.Attributes)
                .HasForeignKey(a => a.InventoryItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // NOTE: Avoid DateTime.UtcNow in seed data for EF migrations (non-deterministic).
            // Seed data can be added via runtime seeding if desired.
        }
    }
}
