using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public enum RoleType
    {
        System,
        Application,
        Workspace
    }

    public class Role : ITenantEntity
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        public RoleType RoleType { get; set; }

        // For workspace roles, we might want to track which workspace they belong to
        public string? WorkspaceId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public int TenantId { get; set; }
    }
}
