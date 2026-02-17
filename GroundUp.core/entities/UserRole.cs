using GroundUp.Core.interfaces;

namespace GroundUp.core.entities
{
    public class UserRole : ITenantEntity
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public int RoleId { get; set; }

        public int TenantId { get; set; }

        public Role Role { get; set; } = null!;
    }
}