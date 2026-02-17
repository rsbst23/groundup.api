using GroundUp.core;
using GroundUp.core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces;

/// <summary>
/// Persistence-only operations needed for enterprise provisioning.
/// This keeps services from depending on EF `DbContext` directly.
/// </summary>
public interface IEnterpriseTenantProvisioningRepository
{
    Task<ApiResponse<int>> CreateEnterpriseTenantAsync(EnterpriseSignupRequestDto request, string realmName, CancellationToken ct = default);
}
