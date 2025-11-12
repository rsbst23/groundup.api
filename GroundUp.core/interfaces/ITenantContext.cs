using System;

namespace GroundUp.core.interfaces
{
    public interface ITenantContext
    {
        Guid TenantId { get; }
    }
}
