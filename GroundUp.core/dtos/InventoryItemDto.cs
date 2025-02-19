namespace GroundUp.core.dtos
{
    public class InventoryItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public string Condition { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; }
        public int InventoryCategoryId { get; set; }
        public string InventoryCategoryName { get; set; } = string.Empty;
        public List<InventoryAttributeDto> Attributes { get; set; } = new();
    }
}
