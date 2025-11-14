using System;
using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.entities
{
    public class UserTenant
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int TenantId { get; set; }

        public Tenant? Tenant { get; set; }
    }
}
