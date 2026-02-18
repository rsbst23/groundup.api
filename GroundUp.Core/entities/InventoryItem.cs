using GroundUp.Core.interfaces;
using System.ComponentModel.DataAnnotations;

namespace GroundUp.Core.entities
{
    public class InventoryItem : ITenantEntity
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public required string Name { get; set; }

        public int InventoryCategoryId { get; set; }
        public decimal PurchasePrice { get; set; }

        public required string Condition { get; set; }

        public DateTime PurchaseDate { get; set; }

        public InventoryCategory InventoryCategory { get; set; } = default!;

        public ICollection<InventoryAttribute> Attributes { get; set; } = new List<InventoryAttribute>();

        public int TenantId { get; set; }
    }
}
