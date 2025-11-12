namespace GroundUp.core.entities
{
    public class UserRole : ITenantEntity
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public int RoleId { get; set; }

        public Guid TenantId { get; set; }

        public Role Role { get; set; } = null!;
    }
}