using GroundUp.core.enums;

namespace GroundUp.core.dtos.tenants
{
    /// <summary>
    /// DTO for creating a new tenant.
    /// </summary>
    public class CreateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentTenantId { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Tenant type: Standard or Enterprise
        /// </summary>
        public TenantType TenantType { get; set; } = TenantType.Standard;

        /// <summary>
        /// Domain for this tenant's application.
        /// Required for enterprise tenants, optional for standard tenants.
        /// </summary>
        public string? CustomDomain { get; set; }

        /// <summary>
        /// List of email domains allowed for SSO auto-join.
        /// </summary>
        public List<string>? SsoAutoJoinDomains { get; set; }

        /// <summary>
        /// Default role ID for auto-joined users.
        /// </summary>
        public int? SsoAutoJoinRoleId { get; set; }
    }
}
