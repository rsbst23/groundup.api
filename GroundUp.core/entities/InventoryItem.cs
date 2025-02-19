namespace GroundUp.core.entities
{
    public class InventoryItem
    {
        public int Id { get; set; }
        public int InventoryCategoryId { get; set; }
        public required string Name { get; set; }
        public decimal PurchasePrice { get; set; }
        public required string Condition { get; set; }
        public DateTime PurchaseDate { get; set; }

        public InventoryCategory InventoryCategory { get; set; } = null!;
        public ICollection<InventoryAttribute> Attributes { get; set; } = new List<InventoryAttribute>();
    }
}
