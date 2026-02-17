using GroundUp.Core.enums;

namespace GroundUp.Core.dtos.tenants
{
    /// <summary>
    /// Read DTO for tenant list views (tables, dropdowns, etc.).
    /// </summary>
    public class TenantListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentTenantId { get; set; }
        public string? ParentTenantName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public TenantType TenantType { get; set; } = TenantType.Standard;
        public string? CustomDomain { get; set; }
        public string RealmName { get; set; } = "groundup";
    }
}
