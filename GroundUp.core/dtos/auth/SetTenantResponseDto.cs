using GroundUp.Core.dtos.tenants;

namespace GroundUp.Core.dtos.auth
{
    /// <summary>
    /// Response DTO for tenant selection after authentication.
    /// Used by POST /api/auth/set-tenant.
    /// </summary>
    public class SetTenantResponseDto
    {
        public bool SelectionRequired { get; set; }
        public List<TenantListItemDto>? AvailableTenants { get; set; }
        public string? Token { get; set; }
    }
}
