namespace GroundUp.core.entities
{
    public class InventoryCategory
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    }
}