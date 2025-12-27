using GroundUp.core.enums;

namespace GroundUp.core.dtos.tenants
{
    /// <summary>
    /// Read DTO for tenant detail views and editing screens.
    /// Kept flat (no nested ParentTenant DTO graphs).
    /// </summary>
    public class TenantDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentTenantId { get; set; }
        public string? ParentTenantName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Tenant type: Standard or Enterprise
        /// </summary>
        public TenantType TenantType { get; set; } = TenantType.Standard;

        /// <summary>
        /// Domain where this tenant's application is hosted
        /// Examples: "acme.yourapp.com", "app.acmecorp.com"
        /// </summary>
        public string? CustomDomain { get; set; }

        /// <summary>
        /// Realm name used for authentication
        /// </summary>
        public string RealmName { get; set; } = "groundup";

        /// <summary>
        /// List of email domains allowed for auto-join via SSO
        /// </summary>
        public List<string>? SsoAutoJoinDomains { get; set; }

        /// <summary>
        /// Default role ID for auto-joined users
        /// </summary>
        public int? SsoAutoJoinRoleId { get; set; }

        /// <summary>
        /// Name of the auto-join role (for display purposes)
        /// </summary>
        public string? SsoAutoJoinRoleName { get; set; }
    }
}
