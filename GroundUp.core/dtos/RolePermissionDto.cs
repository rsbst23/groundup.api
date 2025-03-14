namespace GroundUp.core.dtos
{
    public class RolePermissionDto
    {
        public int Id { get; set; }
        public required string RoleName { get; set; }
        public int PermissionId { get; set; }
        public string? PermissionName { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
