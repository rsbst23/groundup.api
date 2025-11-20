using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    /// <summary>
    /// Represents a user in the system.
    /// Users are created in Keycloak and synced to this table.
    /// Users belong to tenants via the UserTenants junction table (many-to-many).
    /// 
    /// NOTE: User does NOT implement ITenantEntity because users can belong to multiple tenants.
    /// Tenant relationships are managed through the UserTenants junction table.
    /// </summary>
    public class User
    {
        /// <summary>
        /// User ID from Keycloak (matches Keycloak user ID)
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Username (from Keycloak)
        /// </summary>
        public required string Username { get; set; }

        /// <summary>
        /// Email address (from Keycloak)
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// First name (from Keycloak, may be null for social auth)
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Last name (from Keycloak, may be null for social auth)
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Whether user is active/enabled
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// When user was created in our database (not Keycloak timestamp)
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last login timestamp
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Tenants this user belongs to (many-to-many relationship)
        /// </summary>
        public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();

        /// <summary>
        /// User roles (many-to-many relationship)
        /// </summary>
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
