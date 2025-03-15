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

        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        public DbSet<ErrorFeedback> ErrorFeedback { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Permission>()
               .HasIndex(p => p.Name)
               .IsUnique();

            modelBuilder.Entity<RolePermission>()
                .HasIndex(rp => new { rp.RoleName, rp.PermissionId })
                .IsUnique();

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

            modelBuilder.Entity<ErrorFeedback>()
                .Property(e => e.CreatedDate)
                .HasColumnType("DATETIME(6)")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<ErrorFeedback>()
                .Property(e => e.ErrorJson)
                .HasColumnType("LONGTEXT");

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

            // Seed default permissions
            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = 1, Name = "inventory.view", Description = "View inventory items", Group = "Inventory" },
                new Permission { Id = 2, Name = "inventory.create", Description = "Create inventory items", Group = "Inventory" },
                new Permission { Id = 3, Name = "inventory.update", Description = "Update inventory items", Group = "Inventory" },
                new Permission { Id = 4, Name = "inventory.delete", Description = "Delete inventory items", Group = "Inventory" },
                new Permission { Id = 5, Name = "inventory.export", Description = "Export inventory data", Group = "Inventory" },
                new Permission { Id = 6, Name = "errors.view", Description = "View error logs", Group = "Errors" },
                new Permission { Id = 7, Name = "errors.update", Description = "Update error logs", Group = "Errors" },
                new Permission { Id = 8, Name = "errors.delete", Description = "Delete error logs", Group = "Errors" }
            );

            // Seed role-permission mappings for admin role
            modelBuilder.Entity<RolePermission>().HasData(
                new RolePermission { Id = 1, RoleName = "ADMIN", PermissionId = 1 },
                new RolePermission { Id = 2, RoleName = "ADMIN", PermissionId = 2 },
                new RolePermission { Id = 3, RoleName = "ADMIN", PermissionId = 3 },
                new RolePermission { Id = 4, RoleName = "ADMIN", PermissionId = 4 },
                new RolePermission { Id = 5, RoleName = "ADMIN", PermissionId = 5 },
                new RolePermission { Id = 6, RoleName = "ADMIN", PermissionId = 6 },
                new RolePermission { Id = 7, RoleName = "ADMIN", PermissionId = 7 },
                new RolePermission { Id = 8, RoleName = "ADMIN", PermissionId = 8 }
            );

            // Additional permissions for role and user management
            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = 9, Name = "roles.view", Description = "View roles", Group = "Roles" },
                new Permission { Id = 10, Name = "roles.create", Description = "Create roles", Group = "Roles" },
                new Permission { Id = 11, Name = "roles.update", Description = "Update roles", Group = "Roles" },
                new Permission { Id = 12, Name = "roles.delete", Description = "Delete roles", Group = "Roles" },
                new Permission { Id = 13, Name = "roles.permissions.view", Description = "View role permissions", Group = "Roles" },
                new Permission { Id = 14, Name = "roles.permissions.assign", Description = "Assign permissions to roles", Group = "Roles" },
                new Permission { Id = 15, Name = "roles.permissions.remove", Description = "Remove permissions from roles", Group = "Roles" },
                new Permission { Id = 16, Name = "users.view", Description = "View users", Group = "Users" },
                new Permission { Id = 17, Name = "users.roles.view", Description = "View user roles", Group = "Users" },
                new Permission { Id = 18, Name = "users.roles.assign", Description = "Assign roles to users", Group = "Users" },
                new Permission { Id = 19, Name = "users.roles.remove", Description = "Remove roles from users", Group = "Users" }
            );

            // Give the ADMIN role all the new permissions as well
            modelBuilder.Entity<RolePermission>().HasData(
                new RolePermission { Id = 9, RoleName = "ADMIN", PermissionId = 9 },
                new RolePermission { Id = 10, RoleName = "ADMIN", PermissionId = 10 },
                new RolePermission { Id = 11, RoleName = "ADMIN", PermissionId = 11 },
                new RolePermission { Id = 12, RoleName = "ADMIN", PermissionId = 12 },
                new RolePermission { Id = 13, RoleName = "ADMIN", PermissionId = 13 },
                new RolePermission { Id = 14, RoleName = "ADMIN", PermissionId = 14 },
                new RolePermission { Id = 15, RoleName = "ADMIN", PermissionId = 15 },
                new RolePermission { Id = 16, RoleName = "ADMIN", PermissionId = 16 },
                new RolePermission { Id = 17, RoleName = "ADMIN", PermissionId = 17 },
                new RolePermission { Id = 18, RoleName = "ADMIN", PermissionId = 18 },
                new RolePermission { Id = 19, RoleName = "ADMIN", PermissionId = 19 }
            );
        }
    }
}
