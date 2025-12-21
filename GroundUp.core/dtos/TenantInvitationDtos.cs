using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.dtos
{
    /// <summary>
    /// DTO for creating a new tenant invitation
    /// TenantId is automatically determined from ITenantContext
    /// </summary>
    public class CreateTenantInvitationDto
    {
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public required string Email { get; set; }
        
        // TenantId is set automatically from ITenantContext - not passed by user
        [System.Text.Json.Serialization.JsonIgnore]
        public int TenantId { get; set; }
        
        public bool IsAdmin { get; set; } = false;
        
        [Range(1, 365)]
        public int ExpirationDays { get; set; } = 7;
        
        /// <summary>
        /// Indicates if this invitation is for a local account (password-based)
        /// - TRUE: Creates Keycloak user and sends execute-actions email (password setup)
        /// - FALSE: No Keycloak user created upfront - user will authenticate via SSO (Google, Azure AD, etc.)
        /// Default: TRUE for backward compatibility
        /// </summary>
        public bool IsLocalAccount { get; set; } = true;
    }

    /// <summary>
    /// DTO for updating an existing tenant invitation
    /// </summary>
    public class UpdateTenantInvitationDto
    {
        [Required]
        public int Id { get; set; }

        public bool IsAdmin { get; set; } = false;
        
        [Range(1, 365)]
        public int ExpirationDays { get; set; } = 7;
    }

    /// <summary>
    /// DTO for tenant invitation details (response)
    /// </summary>
    public class TenantInvitationDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? RealmName { get; set; }
        public string InvitationToken { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Pending", "Accepted", "Revoked", "Expired"
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public Guid? AcceptedByUserId { get; set; }
        public string? AcceptedByUserName { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsAdmin { get; set; }
    }

    /// <summary>
    /// DTO for accepting an invitation
    /// </summary>
    public class AcceptInvitationDto
    {
        [Required]
        [MaxLength(100)]
        public required string InvitationToken { get; set; }
    }
}
