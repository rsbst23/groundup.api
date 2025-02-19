namespace GroundUp.core.entities
{
    public class InventoryCategory
    {
        public int Id { get; set; }
        public required string Name { get; set; }  // Required, avoids warning
        public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    }
}