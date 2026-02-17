using GroundUp.Core.entities;

namespace GroundUp.Core.dtos
{
    public class RolePolicyAssignmentDto
    {
        public required string RoleName { get; set; }
        public RoleType RoleType { get; set; }
        public int PolicyId { get; set; }
    }
}
