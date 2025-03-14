using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class RolePermission
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public required string RoleName { get; set; }

        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;

        // When the mapping was created
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
