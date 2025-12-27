namespace GroundUp.core.dtos.auth
{
    /// <summary>
    /// Request DTO for selecting a tenant after authentication.
    /// Used by POST /api/auth/set-tenant.
    /// </summary>
    public class SetTenantRequestDto
    {
        public int? TenantId { get; set; }
    }
}
