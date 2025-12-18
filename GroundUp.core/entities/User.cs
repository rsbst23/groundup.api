using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    /// <summary>
    /// Represents a global logical user in the GroundUp system.
    /// Users belong to tenants via the UserTenants junction table (many-to-many).
    /// Identity mapping is handled via UserTenant.ExternalUserId (Keycloak sub claim).
    /// 
    /// NOTE: User does NOT implement ITenantEntity because users can belong to multiple tenants.
    /// Tenant relationships are managed through the UserTenants junction table.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Global GroundUp user ID (not tied to any specific Keycloak user ID)
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Display name for this user (can be synced from Keycloak or set manually)
        /// </summary>
        [MaxLength(255)]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Username (from Keycloak preferred_username claim)
        /// NULLABLE: Not all authentication methods provide username
        /// </summary>
        [MaxLength(255)]
        public string? Username { get; set; }

        /// <summary>
        /// Email address (from Keycloak email claim)
        /// NULLABLE: Not all authentication methods provide email (e.g., some social logins, enterprise SSO)
        /// Used for notifications when available, but NOT for authentication/identity
        /// </summary>
        [MaxLength(255)]
        public string? Email { get; set; }

        /// <summary>
        /// First name (from Keycloak, may be null for social auth)
        /// </summary>
        [MaxLength(255)]
        public string? FirstName { get; set; }

        /// <summary>
        /// Last name (from Keycloak, may be null for social auth)
        /// </summary>
        [MaxLength(255)]
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
        /// When user was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

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
