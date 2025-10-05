namespace GroundUp.core.dtos
{
    public class PolicyPermissionAssignmentDto
    {
        public int PolicyId { get; set; }
        public List<int> PermissionIds { get; set; } = new List<int>();
    }
}
