using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class InventoryCategory : ITenantEntity
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public required string Name { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();

        public int TenantId { get; set; }
    }
}