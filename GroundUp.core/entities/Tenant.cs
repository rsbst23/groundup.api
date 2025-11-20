using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    /// <summary>
    /// Represents a tenant (organization) in the system.
    /// Supports hierarchical tenants (parent/child relationships).
    /// </summary>
    public class Tenant
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Tenant name
        /// </summary>
        [Required]
        [MaxLength(255)]
        public required string Name { get; set; }

        /// <summary>
        /// Tenant description
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Parent tenant ID for hierarchical tenants.
        /// Null if this is a root tenant.
        /// Example: Service provider (parent) ? Customer tenants (children)
        /// </summary>
        public int? ParentTenantId { get; set; }

        /// <summary>
        /// When tenant was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether tenant is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        // Navigation properties
        
        /// <summary>
        /// Users belonging to this tenant (many-to-many)
        /// </summary>
        public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();

        /// <summary>
        /// Parent tenant (for hierarchical tenants)
        /// </summary>
        public Tenant? ParentTenant { get; set; }

        /// <summary>
        /// Child tenants (for hierarchical tenants)
        /// </summary>
        public ICollection<Tenant> ChildTenants { get; set; } = new List<Tenant>();
    }
}
