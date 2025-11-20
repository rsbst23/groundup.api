using System;
using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    /// <summary>
    /// Junction table linking users to tenants.
    /// Represents a user's membership in a specific tenant.
    /// Supports multi-tenant users (one user can belong to multiple tenants).
    /// </summary>
    public class UserTenant
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// User ID (from Keycloak)
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Tenant ID
        /// </summary>
        public int TenantId { get; set; }

        /// <summary>
        /// Whether this user is an admin for this specific tenant.
        /// Admins can manage users, create invitations, and perform tenant-level administration.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// When user was assigned to this tenant
        /// </summary>
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        /// <summary>
        /// User associated with this tenant membership
        /// </summary>
        public User? User { get; set; }

        /// <summary>
        /// Tenant associated with this membership
        /// </summary>
        public Tenant? Tenant { get; set; }
    }
}
