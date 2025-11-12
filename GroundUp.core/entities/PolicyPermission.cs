using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class PolicyPermission
    {
        public int Id { get; set; }
        public int PolicyId { get; set; }
        public int PermissionId { get; set; }

        // Navigation properties
        public Policy Policy { get; set; } = null!;
        public Permission Permission { get; set; } = null!;
    }
}
