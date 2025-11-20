using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class TenantInvitation
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public required string Email { get; set; }
        
        public int TenantId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public required string InvitationToken { get; set; }
        
        public DateTime ExpiresAt { get; set; }
        public bool IsAccepted { get; set; } = false;
        public DateTime? AcceptedAt { get; set; }
        public Guid? AcceptedByUserId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [MaxLength(50)]
        public string? AssignedRole { get; set; }
        
        public bool IsAdmin { get; set; } = false;
        public string? Metadata { get; set; }
        
        // Navigation properties
        public Tenant? Tenant { get; set; }
        public User? AcceptedByUser { get; set; }
        public User? CreatedByUser { get; set; }
        
        // Computed properties
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        public bool IsValid => !IsAccepted && !IsExpired;
    }
}
