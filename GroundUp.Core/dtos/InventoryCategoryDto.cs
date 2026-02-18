namespace GroundUp.Core.dtos
{
    public class InventoryCategoryDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
