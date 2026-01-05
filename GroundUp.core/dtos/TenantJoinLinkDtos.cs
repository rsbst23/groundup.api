using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.dtos
{
    /// <summary>
    /// DTO for creating a new join link
    /// TenantId is automatically determined from ITenantContext
    /// </summary>
    public class CreateTenantJoinLinkDto
    {
        [Range(1, 365)]
        public int ExpirationDays { get; set; } = 30;
        
        /// <summary>
        /// Optional default role to assign when users join via this link
        /// </summary>
        public int? DefaultRoleId { get; set; }
    }

    /// <summary>
    /// DTO for join link details (response)
    /// </summary>
    public class TenantJoinLinkDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string JoinToken { get; set; } = string.Empty;
        public string JoinUrl { get; set; } = string.Empty;
        public bool IsRevoked { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? DefaultRoleId { get; set; }

        /// <summary>
        /// Optional tenant name for display purposes (populated for token-based lookups).
        /// </summary>
        public string? TenantName { get; set; }

        /// <summary>
        /// Realm to use for the join-link flow (populated for token-based lookups).
        /// Standard tenants typically use the shared realm (e.g. "groundup");
        /// enterprise tenants use their dedicated realm.
        /// </summary>
        public string? RealmName { get; set; }
    }
}
