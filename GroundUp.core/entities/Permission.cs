using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class Permission
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        // Permissions can be grouped for organization
        [MaxLength(100)]
        public string? Group { get; set; }

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
