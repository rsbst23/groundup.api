namespace GroundUp.core.dtos
{
    /// <summary>
    /// DTO for tenant display and editing
    /// </summary>
    public class TenantDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentTenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Tenant type: Standard or Enterprise
        /// </summary>
        public GroundUp.core.enums.TenantType TenantType { get; set; } = GroundUp.core.enums.TenantType.Standard;

        /// <summary>
        /// Domain where this tenant's application is hosted
        /// Examples: "acme.yourapp.com", "app.acmecorp.com"
        /// </summary>
        public string? CustomDomain { get; set; }

        // Optional navigation property for display
        public string? ParentTenantName { get; set; }

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

    /// <summary>
    /// DTO for creating a new tenant
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
        public GroundUp.core.enums.TenantType TenantType { get; set; } = GroundUp.core.enums.TenantType.Standard;

        /// <summary>
        /// Domain for this tenant's application
        /// Examples: "acme.yourapp.com", "app.acmecorp.com"
        /// Required for enterprise tenants, optional for standard tenants
        /// </summary>
        public string? CustomDomain { get; set; }

        /// <summary>
        /// List of email domains allowed for SSO auto-join
        /// </summary>
        public List<string>? SsoAutoJoinDomains { get; set; }

        /// <summary>
        /// Default role ID for auto-joined users
        /// </summary>
        public int? SsoAutoJoinRoleId { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing tenant
    /// </summary>
    public class UpdateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Domain for this tenant's application
        /// Can be updated for enterprise tenants
        /// </summary>
        public string? CustomDomain { get; set; }

        /// <summary>
        /// List of email domains allowed for SSO auto-join
        /// </summary>
        public List<string>? SsoAutoJoinDomains { get; set; }

        /// <summary>
        /// Default role ID for auto-joined users
        /// </summary>
        public int? SsoAutoJoinRoleId { get; set; }

        // Note: TenantType cannot be changed after creation
    }

    /// <summary>
    /// DTO for configuring SSO auto-join settings
    /// </summary>
    public class ConfigureSsoSettingsDto
    {
        /// <summary>
        /// List of email domains allowed for auto-join
        /// Example: ["acme.com", "acmecorp.com"]
        /// Set to null or empty array to disable auto-join (invitation-only mode)
        /// </summary>
        public List<string>? SsoAutoJoinDomains { get; set; }

        /// <summary>
        /// Default role ID to assign when users auto-join
        /// If null, uses tenant's default member role
        /// </summary>
        public int? SsoAutoJoinRoleId { get; set; }
    }
}
