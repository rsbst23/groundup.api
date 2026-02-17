using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.entities;
using GroundUp.Core.enums;
using GroundUp.Core.interfaces;
using GroundUp.Data.Core.Data;
using Microsoft.AspNetCore.Http;

namespace GroundUp.Data.Core.Repositories;

internal sealed class EnterpriseTenantProvisioningRepository : IEnterpriseTenantProvisioningRepository
{
    private readonly ApplicationDbContext _dbContext;

    public EnterpriseTenantProvisioningRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<int>> CreateEnterpriseTenantAsync(EnterpriseSignupRequestDto request, string realmName, CancellationToken ct = default)
    {
        var tenant = new Tenant
        {
            Name = request.CompanyName,
            TenantType = TenantType.Enterprise,
            RealmName = realmName,
            CustomDomain = request.CustomDomain,
            Plan = request.Plan,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(ct);

        return new ApiResponse<int>(tenant.Id, true, "Enterprise tenant created successfully", null, StatusCodes.Status201Created);
    }
}
