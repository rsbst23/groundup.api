using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GroundUp.core.entities
{
    public class ErrorFeedback : ITenantEntity
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string Feedback { get; set; }

        [MaxLength(255)]
        [EmailAddress]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? Context { get; set; }

        [Required]
        public required string ErrorJson { get; set; }

        [MaxLength(2000)]
        public string? Url { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public DateTime Timestamp { get; set; }
        public DateTime? CreatedDate { get; set; }

        public int TenantId { get; set; }
    }
}