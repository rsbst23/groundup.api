using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class User : ITenantEntity
    {
        public Guid Id { get; set; } // Matches Keycloak's user ID

        public required string Username { get; set; }

        public required string Email { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        public int TenantId { get; set; } // Primary tenant for this user

        // Navigation properties
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
    }
}
