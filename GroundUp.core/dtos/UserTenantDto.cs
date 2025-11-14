using GroundUp.core.entities;

namespace GroundUp.core.dtos
{
    public class UserTenantDto
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int TenantId { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public Tenant Tenant { get; set; }
    }
}
