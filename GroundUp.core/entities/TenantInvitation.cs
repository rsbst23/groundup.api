using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public enum InvitationStatus
    {
        Pending,
        Accepted,
        Revoked,
        Expired
    }

    /// <summary>
    /// Represents an invitation to join a tenant.
    /// Invitations are token-based and require an email address to send the invitation link.
    /// </summary>
    public class TenantInvitation : ITenantEntity
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Tenant being invited to
        /// </summary>
        public int TenantId { get; set; }
        
        /// <summary>
        /// Unique invitation token (secure, random, single-use)
        /// </summary>
        [Required]
        [MaxLength(200)]
        public required string InvitationToken { get; set; }
        
        /// <summary>
        /// Email address to send invitation to
        /// REQUIRED: Needed to send the invitation link
        /// </summary>
        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public required string ContactEmail { get; set; }
        
        /// <summary>
        /// Optional contact name for personalization
        /// </summary>
        [MaxLength(255)]
        public string? ContactName { get; set; }
        
        /// <summary>
        /// Optional role to assign (for application-level roles defined in the tenant)
        /// </summary>
        public int? RoleId { get; set; }

        /// <summary>
        /// Whether invitee should be granted admin privileges (tenant-owner protection)
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Invitation lifecycle status
        /// </summary>
        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

        /// <summary>
        /// When invitation expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }
        
        /// <summary>
        /// When invitation was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When invitation was accepted (null if not accepted)
        /// </summary>
        public DateTime? AcceptedAt { get; set; }
        
        /// <summary>
        /// User who accepted the invitation
        /// </summary>
        public Guid? AcceptedByUserId { get; set; }
        
        /// <summary>
        /// User who created the invitation
        /// Nullable for system-generated invitations (e.g., enterprise signup)
        /// </summary>
        public Guid? CreatedByUserId { get; set; }
        
        /// <summary>
        /// Optional metadata (JSON string for extensibility)
        /// </summary>
        public string? Metadata { get; set; }
        
        // Navigation properties
        public Tenant? Tenant { get; set; }
        public User? AcceptedByUser { get; set; }
        public User? CreatedByUser { get; set; }
        
        // Computed properties
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}
