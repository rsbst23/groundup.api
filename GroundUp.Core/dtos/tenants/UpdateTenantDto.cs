namespace GroundUp.Core.dtos.tenants
{
    /// <summary>
    /// DTO for updating an existing tenant.
    /// </summary>
    public class UpdateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Domain for this tenant's application.
        /// Can be updated for enterprise tenants.
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

        // Note: TenantType cannot be changed after creation.
    }
}
