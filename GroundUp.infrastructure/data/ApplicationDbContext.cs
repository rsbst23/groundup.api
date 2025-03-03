using GroundUp.core.entities;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Book> Books { get; set; }

        public DbSet<InventoryCategory> InventoryCategories { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<InventoryAttribute> InventoryAttributes { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<InventoryCategory>()
                .Property(b => b.CreatedDate)
                .HasColumnType("DATETIME(6)")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAdd();

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

            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Name = "Alice", Email = "alice@example.com" },
                new User { Id = 2, Name = "Bob", Email = "bob@example.com" }
            );

            modelBuilder.Entity<InventoryCategory>().HasData(
                new InventoryCategory { Id = 1, Name = "Electronics", CreatedDate = null },
                new InventoryCategory { Id = 2, Name = "Books", CreatedDate = null }
            );

            modelBuilder.Entity<InventoryItem>().HasData(
                new InventoryItem { Id = 1, Name = "Laptop", PurchasePrice = 999.99m, Condition = "New", InventoryCategoryId = 1, PurchaseDate = DateTime.UtcNow },
                new InventoryItem { Id = 2, Name = "The Great Gatsby", PurchasePrice = 12.99m, Condition = "Used", InventoryCategoryId = 2, PurchaseDate = DateTime.UtcNow }
            );
        }
    }
}
