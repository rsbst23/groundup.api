using GroundUp.Core.dtos;
using GroundUp.Core.dtos.tenants;

namespace GroundUp.Core.interfaces
{
    public interface ITenantSsoSettingsService
    {
        /// <summary>
        /// Configures SSO auto-join settings for an enterprise tenant.
        /// Orchestrates validation + persistence and returns updated tenant details.
        /// </summary>
        Task<ApiResponse<TenantDetailDto>> ConfigureSsoSettingsAsync(int tenantId, ConfigureSsoSettingsDto dto);
    }
}
