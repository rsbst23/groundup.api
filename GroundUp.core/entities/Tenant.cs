using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using GroundUp.Core.enums;

namespace GroundUp.Core.entities
{
    public class Tenant
    {
        private const string DEFAULT_REALM = "groundup";

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
        /// When tenant was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Whether tenant is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Tenant type: Standard or Enterprise
        /// </summary>
        public TenantType TenantType { get; set; } = TenantType.Standard;

        /// <summary>
        /// Subscription/billing plan (e.g., 'free', 'pro', 'enterprise')
        /// </summary>
        [MaxLength(100)]
        public string? Plan { get; set; }

        /// <summary>
        /// Keycloak realm name for this tenant.
        /// </summary>
        [MaxLength(255)]
        public string RealmName { get; set; } = DEFAULT_REALM;

        /// <summary>
        /// Domain where this tenant's application is hosted
        /// Used for:
        /// - Realm resolution (lookup by URL)
        /// - Keycloak redirect URIs
        /// - Email invitation links
        /// </summary>
        [MaxLength(255)]
        public string? CustomDomain { get; set; }

        /// <summary>
        /// Onboarding mode for this tenant. Default = InvitationsRequired.
        /// </summary>
        public OnboardingMode Onboarding { get; set; } = OnboardingMode.InvitationsRequired;

        /// <summary>
        /// JSON array of email domains allowed for SSO auto-join
        /// Example JSON: ["acme.com", "acmecorp.com"]
        /// If null/empty: All users require explicit invitation
        /// If populated: Users from these domains can auto-join on first SSO login
        /// </summary>
        public string? SsoAutoJoinDomainsJson { get; set; }
        
        /// <summary>
        /// Parsed list of allowed domains (not mapped to database)
        /// Use this property in application code
        /// </summary>
        [NotMapped]
        public List<string>? SsoAutoJoinDomains
        {
            get => string.IsNullOrEmpty(SsoAutoJoinDomainsJson)
                ? null
                : JsonSerializer.Deserialize<List<string>>(SsoAutoJoinDomainsJson);
            set => SsoAutoJoinDomainsJson = value == null || value.Count == 0
                ? null
                : JsonSerializer.Serialize(value);
        }
        
        /// <summary>
        /// Default role ID assigned when users auto-join via allowed domain
        /// If null: Uses tenant's default member role (e.g., "Member")
        /// </summary>
        public int? SsoAutoJoinRoleId { get; set; }
        
        /// <summary>
        /// Navigation property for auto-join role
        /// </summary>
        public Role? SsoAutoJoinRole { get; set; }

        // Navigation properties
        public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
        public Tenant? ParentTenant { get; set; }
        public ICollection<Tenant> ChildTenants { get; set; } = new List<Tenant>();
    }
}
