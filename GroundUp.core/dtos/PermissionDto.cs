namespace GroundUp.core.dtos
{
    public class PermissionDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? Group { get; set; }
    }
}
