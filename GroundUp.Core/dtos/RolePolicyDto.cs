using GroundUp.Core.entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroundUp.Core.dtos
{
    public class RolePolicyDto
    {
        public int Id { get; set; }
        public required string RoleName { get; set; }
        public RoleType RoleType { get; set; }
        public int PolicyId { get; set; }
        public string? PolicyName { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
