using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;
using GroundUp.core.interfaces;

namespace GroundUp.infrastructure.services
{
    public class TenantSsoSettingsService : ITenantSsoSettingsService
    {
        private readonly ITenantSsoSettingsRepository _tenantSsoSettingsRepository;
        private readonly ITenantContext _tenantContext;
        private readonly IMapper _mapper;
        private readonly ILoggingService _logger;

        public TenantSsoSettingsService(
            ITenantSsoSettingsRepository tenantSsoSettingsRepository,
            ITenantContext tenantContext,
            IMapper mapper,
            ILoggingService logger)
        {
            _tenantSsoSettingsRepository = tenantSsoSettingsRepository;
            _tenantContext = tenantContext;
            _mapper = mapper;
            _logger = logger;
        }

        public Task<ApiResponse<TenantDetailDto>> ConfigureSsoSettingsAsync(int tenantId, ConfigureSsoSettingsDto dto)
        {
            return _tenantSsoSettingsRepository.ConfigureSsoSettingsAsync(tenantId, dto);
        }
    }
}
