using GroundUp.Core.interfaces;
using System.ComponentModel.DataAnnotations;

namespace GroundUp.Core.entities
{
    public class TenantJoinLink : ITenantEntity
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        /// <summary>
        /// Unique join token (GUID string or similar). We'll store as string for flexibility.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public required string JoinToken { get; set; }

        public bool IsRevoked { get; set; } = false;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? DefaultRoleId { get; set; }

        // Navigation
        public Tenant? Tenant { get; set; }
    }
}
