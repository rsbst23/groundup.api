using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class User : ITenantEntity
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public required string Email { get; set; }

        public Guid TenantId { get; set; }
    }
}
