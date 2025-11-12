namespace GroundUp.core.entities
{
    public class RolePermission : ITenantEntity
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public int PermissionId { get; set; }
        public Guid TenantId { get; set; }
    }
}
