namespace GroundUp.core.entities
{
    public class UserRole
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public int RoleId { get; set; }

        public Role Role { get; set; } = null!;
    }
}