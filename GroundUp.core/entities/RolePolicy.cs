using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroundUp.core.entities
{
    public enum RoleType
    {
        System,
        Application,
        Workspace
    }

    public class RolePolicy
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public required string RoleName { get; set; }

        public RoleType RoleType { get; set; }
        public int PolicyId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Policy Policy { get; set; } = null!;
    }
}
