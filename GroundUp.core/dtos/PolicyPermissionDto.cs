namespace GroundUp.core.dtos
{
    public class PolicyPermissionDto
    {
        public int Id { get; set; }
        public int PolicyId { get; set; }
        public int PermissionId { get; set; }
        public string? PolicyName { get; set; }
        public string? PermissionName { get; set; }
    }
}
