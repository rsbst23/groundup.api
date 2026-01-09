using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;

namespace GroundUp.core.interfaces;

public interface ITenantSsoSettingsRepository
{
    Task<ApiResponse<TenantDetailDto>> ConfigureSsoSettingsAsync(int tenantId, ConfigureSsoSettingsDto dto);
}
