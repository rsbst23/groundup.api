using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class Tenant
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public required string Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
    }
}
