using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class Book : ITenantEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Author { get; set; } = string.Empty;

        public DateTime? PublishedDate { get; set; }

        public int TenantId { get; set; }
    }
}
