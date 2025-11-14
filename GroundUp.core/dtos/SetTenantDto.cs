using System;
using System.Collections.Generic;
using GroundUp.core.entities;

namespace GroundUp.core.dtos
{
    public class SetTenantRequestDto
    {
        public int? TenantId { get; set; }
    }

    public class SetTenantResponseDto
    {
        public bool SelectionRequired { get; set; }
        public List<Tenant>? AvailableTenants { get; set; }
        public string? Token { get; set; }
    }
}
