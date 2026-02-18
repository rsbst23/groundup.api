using GroundUp.Core.dtos;
using GroundUp.Core.dtos.tenants;

namespace GroundUp.Data.Abstractions.Interfaces;

public interface ITenantSsoSettingsRepository
{
    Task<ApiResponse<TenantDetailDto>> ConfigureSsoSettingsAsync(int tenantId, ConfigureSsoSettingsDto dto);
}
