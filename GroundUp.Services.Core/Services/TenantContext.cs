using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Http;

namespace GroundUp.Services.Core.Services;

public sealed class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int TenantId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.User?.Identity?.IsAuthenticated == true)
            {
                var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
                if (int.TryParse(tenantIdClaim, out var tenantId))
                {
                    return tenantId;
                }
            }

            throw new InvalidOperationException("TenantId claim is missing or invalid.");
        }
    }
}
