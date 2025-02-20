namespace GroundUp.core.entities
{
    public class InventoryAttribute
    {
        public int Id { get; set; }
        public int InventoryItemId { get; set; }
        public required string FieldName { get; set; }
        public required string FieldValue { get; set; }

        public InventoryItem InventoryItem { get; set; } = null!;
    }
}
