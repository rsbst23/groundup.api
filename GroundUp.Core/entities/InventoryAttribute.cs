using GroundUp.Core.interfaces;
using System.ComponentModel.DataAnnotations;

namespace GroundUp.Core.entities
{
    public class InventoryAttribute : ITenantEntity
    {
        public int Id { get; set; }

        public int InventoryItemId { get; set; }

        [Required]
        [MaxLength(255)]
        public required string FieldName { get; set; }

        public string? FieldValue { get; set; }

        public int TenantId { get; set; }

        public InventoryItem InventoryItem { get; set; } = default!;
    }
}
