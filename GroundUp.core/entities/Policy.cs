using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class Policy : ITenantEntity
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        public Guid TenantId { get; set; }

        // Navigation properties
        public ICollection<PolicyPermission> PolicyPermissions { get; set; } = new List<PolicyPermission>();
        public ICollection<RolePolicy> RolePolicies { get; set; } = new List<RolePolicy>();
    }
}
