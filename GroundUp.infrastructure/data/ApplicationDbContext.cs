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
        public DbSet<Policy> Policies { get; set; }
        public DbSet<PolicyPermission> PolicyPermissions { get; set; }
        public DbSet<RolePolicy> RolePolicies { get; set; }
        public DbSet<Role> Roles { get; set; }

        public DbSet<ErrorFeedback> ErrorFeedback { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Permission>()
               .HasIndex(p => p.Name)
               .IsUnique();

            modelBuilder.Entity<Policy>()
                .HasIndex(p => p.Name)
                .IsUnique();

            modelBuilder.Entity<PolicyPermission>()
               .HasKey(pp => pp.Id);

            modelBuilder.Entity<PolicyPermission>()
                .HasIndex(pp => new { pp.PolicyId, pp.PermissionId })
                .IsUnique();

            modelBuilder.Entity<PolicyPermission>()
                .HasOne(pp => pp.Policy)
                .WithMany(p => p.PolicyPermissions)
                .HasForeignKey(pp => pp.PolicyId);

            modelBuilder.Entity<PolicyPermission>()
                .HasOne(pp => pp.Permission)
                .WithMany(p => p.PolicyPermissions)
                .HasForeignKey(pp => pp.PermissionId);

            modelBuilder.Entity<RolePolicy>()
                            .HasKey(rp => rp.Id);

            modelBuilder.Entity<RolePolicy>()
                .HasIndex(rp => new { rp.RoleName, rp.RoleType, rp.PolicyId })
                .IsUnique();

            modelBuilder.Entity<RolePolicy>()
                .HasOne(rp => rp.Policy)
                .WithMany(p => p.RolePolicies)
                .HasForeignKey(rp => rp.PolicyId);

            modelBuilder.Entity<Role>()
                .HasIndex(r => new { r.Name, r.RoleType })
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

            modelBuilder.Entity<UserRole>()
                .HasKey(ur => ur.Id);

            modelBuilder.Entity<UserRole>()
                .HasIndex(ur => new { ur.UserId, ur.RoleId })
                .IsUnique();

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany()
                .HasForeignKey(ur => ur.RoleId);

            // Seed Users
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Name = "Alice", Email = "alice@example.com" },
                new User { Id = 2, Name = "Bob", Email = "bob@example.com" }
            );

            // Seed Inventory Categories
            modelBuilder.Entity<InventoryCategory>().HasData(
                new InventoryCategory { Id = 1, Name = "Electronics", CreatedDate = null },
                new InventoryCategory { Id = 2, Name = "Books", CreatedDate = null }
            );

            // Seed Inventory Items
            modelBuilder.Entity<InventoryItem>().HasData(
                new InventoryItem { Id = 1, Name = "Laptop", PurchasePrice = 999.99m, Condition = "New", InventoryCategoryId = 1, PurchaseDate = DateTime.UtcNow },
                new InventoryItem { Id = 2, Name = "The Great Gatsby", PurchasePrice = 12.99m, Condition = "Used", InventoryCategoryId = 2, PurchaseDate = DateTime.UtcNow }
            );

            // Seed Permissions
            modelBuilder.Entity<Permission>().HasData(
                // Inventory permissions
                new Permission { Id = 1, Name = "inventory.view", Description = "View inventory items", Group = "Inventory" },
                new Permission { Id = 2, Name = "inventory.create", Description = "Create inventory items", Group = "Inventory" },
                new Permission { Id = 3, Name = "inventory.update", Description = "Update inventory items", Group = "Inventory" },
                new Permission { Id = 4, Name = "inventory.delete", Description = "Delete inventory items", Group = "Inventory" },
                new Permission { Id = 5, Name = "inventory.export", Description = "Export inventory data", Group = "Inventory" },

                // Error permissions
                new Permission { Id = 6, Name = "errors.view", Description = "View error logs", Group = "Errors" },
                new Permission { Id = 7, Name = "errors.update", Description = "Update error logs", Group = "Errors" },
                new Permission { Id = 8, Name = "errors.delete", Description = "Delete error logs", Group = "Errors" },

                // Role permissions
                new Permission { Id = 9, Name = "roles.view", Description = "View roles", Group = "Roles" },
                new Permission { Id = 10, Name = "roles.create", Description = "Create roles", Group = "Roles" },
                new Permission { Id = 11, Name = "roles.update", Description = "Update roles", Group = "Roles" },
                new Permission { Id = 12, Name = "roles.delete", Description = "Delete roles", Group = "Roles" },
                new Permission { Id = 13, Name = "roles.permissions.view", Description = "View role permissions", Group = "Roles" },
                new Permission { Id = 14, Name = "roles.permissions.assign", Description = "Assign permissions to roles", Group = "Roles" },
                new Permission { Id = 15, Name = "roles.permissions.remove", Description = "Remove permissions from roles", Group = "Roles" },

                // Policy permissions
                new Permission { Id = 16, Name = "policies.view", Description = "View policies", Group = "Policies" },
                new Permission { Id = 17, Name = "policies.create", Description = "Create policies", Group = "Policies" },
                new Permission { Id = 18, Name = "policies.update", Description = "Update policies", Group = "Policies" },
                new Permission { Id = 19, Name = "policies.delete", Description = "Delete policies", Group = "Policies" },

                // User permissions
                new Permission { Id = 20, Name = "users.view", Description = "View users", Group = "Users" },
                new Permission { Id = 21, Name = "users.roles.view", Description = "View user roles", Group = "Users" },
                new Permission { Id = 22, Name = "users.roles.assign", Description = "Assign roles to users", Group = "Users" },
                new Permission { Id = 23, Name = "users.roles.remove", Description = "Remove roles from users", Group = "Users" }
            );
        }
    }
}