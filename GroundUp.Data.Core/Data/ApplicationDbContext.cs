using GroundUp.Core.entities;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.Data.Core.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }

    public DbSet<Permission> Permissions { get; set; }
    public DbSet<Policy> Policies { get; set; }
    public DbSet<PolicyPermission> PolicyPermissions { get; set; }
    public DbSet<RolePolicy> RolePolicies { get; set; }
    public DbSet<Role> Roles { get; set; }

    public DbSet<ErrorFeedback> ErrorFeedback { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }

    // Tenant and user-tenant entities
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<UserTenant> UserTenants { get; set; }
    public DbSet<TenantInvitation> TenantInvitations { get; set; }
    public DbSet<TenantJoinLink> TenantJoinLinks { get; set; }

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

        modelBuilder.Entity<ErrorFeedback>()
            .Property(e => e.CreatedDate)
            .HasColumnType("DATETIME(6)");

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

        // Tenant configuration
        modelBuilder.Entity<Tenant>()
            .HasKey(t => t.Id);

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.Name)
            .IsUnique();

        // Tenant hierarchical relationship (self-referencing)
        modelBuilder.Entity<Tenant>()
            .HasOne(t => t.ParentTenant)
            .WithMany(t => t.ChildTenants)
            .HasForeignKey(t => t.ParentTenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tenant>()
            .Property(t => t.CreatedAt)
            .HasColumnType("DATETIME(6)");

        // Tenant SSO auto-join configuration
        modelBuilder.Entity<Tenant>()
            .HasOne(t => t.SsoAutoJoinRole)
            .WithMany()
            .HasForeignKey(t => t.SsoAutoJoinRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Tenant>()
            .Property(t => t.SsoAutoJoinDomainsJson)
            .HasColumnName("SsoAutoJoinDomains");

        // UserTenant configuration
        modelBuilder.Entity<UserTenant>()
            .HasKey(ut => ut.Id);

        modelBuilder.Entity<UserTenant>()
            .HasIndex(ut => new { ut.UserId, ut.TenantId })
            .IsUnique();

        modelBuilder.Entity<UserTenant>()
            .HasIndex(ut => new { ut.TenantId, ut.ExternalUserId });

        modelBuilder.Entity<UserTenant>()
            .HasOne(ut => ut.Tenant)
            .WithMany(t => t.UserTenants)
            .HasForeignKey(ut => ut.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserTenant>()
            .HasOne(ut => ut.User)
            .WithMany(u => u.UserTenants)
            .HasForeignKey(ut => ut.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserTenant>()
            .Property(ut => ut.JoinedAt)
            .HasColumnType("DATETIME(6)");

        // User configuration
        modelBuilder.Entity<User>()
            .HasKey(u => u.Id);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username);

        modelBuilder.Entity<User>()
            .Property(u => u.CreatedAt)
            .HasColumnType("DATETIME(6)");

        modelBuilder.Entity<User>()
            .Property(u => u.UpdatedAt)
            .HasColumnType("DATETIME(6)");

        modelBuilder.Entity<User>()
            .Property(u => u.LastLoginAt)
            .HasColumnType("DATETIME(6)");

        // TenantInvitation configuration
        modelBuilder.Entity<TenantInvitation>()
            .HasKey(ti => ti.Id);

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
            new Permission { Id = 21, Name = "users.create", Description = "Create users", Group = "Users" },
            new Permission { Id = 22, Name = "users.update", Description = "Update users", Group = "Users" },
            new Permission { Id = 23, Name = "users.delete", Description = "Delete users", Group = "Users" },
            new Permission { Id = 24, Name = "users.roles.view", Description = "View user roles", Group = "Users" },
            new Permission { Id = 25, Name = "users.roles.assign", Description = "Assign roles to users", Group = "Users" },
            new Permission { Id = 26, Name = "users.roles.remove", Description = "Remove roles from users", Group = "Users" },
            new Permission { Id = 27, Name = "users.tenants.view", Description = "View user tenants", Group = "Users" },
            new Permission { Id = 28, Name = "users.tenants.assign", Description = "Assign users to tenants", Group = "Users" },
            new Permission { Id = 29, Name = "users.tenants.remove", Description = "Remove users from tenants", Group = "Users" }
        );
    }
}
