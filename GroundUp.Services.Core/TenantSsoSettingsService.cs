using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.dtos.tenants;
using GroundUp.Core.interfaces;

namespace GroundUp.Services.Core;

internal sealed class TenantSsoSettingsService : ITenantSsoSettingsService
{
    private readonly ITenantSsoSettingsRepository _tenantSsoSettingsRepository;

    public TenantSsoSettingsService(ITenantSsoSettingsRepository tenantSsoSettingsRepository)
    {
        _tenantSsoSettingsRepository = tenantSsoSettingsRepository;
    }

    public Task<ApiResponse<TenantDetailDto>> ConfigureSsoSettingsAsync(int tenantId, ConfigureSsoSettingsDto dto)
        => _tenantSsoSettingsRepository.ConfigureSsoSettingsAsync(tenantId, dto);
}
