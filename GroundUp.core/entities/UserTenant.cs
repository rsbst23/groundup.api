using GroundUp.Core.interfaces;
using System;
using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    /// <summary>
    /// Junction table linking users to tenants.
    /// Represents a user's membership in a specific tenant.
    /// Supports multi-tenant users (one user can belong to multiple tenants).
    /// </summary>
    public class UserTenant : ITenantEntity
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// User ID (GroundUp)
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Tenant ID
        /// </summary>
        public int TenantId { get; set; }

        /// <summary>
        /// Keycloak (or external IdP) user id / sub for this tenant's realm
        /// Stored here to simplify membership resolution without needing a separate mapping table
        /// </summary>
        [MaxLength(255)]
        public string? ExternalUserId { get; set; }

        /// <summary>
        /// Whether this user is an admin for this specific tenant.
        /// Admins can manage users, create invitations, and perform tenant-level administration.
        /// This is a tenant-owner protection flag and is intentionally separate from role assignments.
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
