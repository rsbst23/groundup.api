using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;
using GroundUp.core.enums;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.services
{
    public class TenantSsoSettingsService : ITenantSsoSettingsService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly ILoggingService _logger;

        public TenantSsoSettingsService(
            ApplicationDbContext dbContext,
            IMapper mapper,
            ILoggingService logger)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<TenantDetailDto>> ConfigureSsoSettingsAsync(int tenantId, ConfigureSsoSettingsDto dto)
        {
            try
            {
                var tenant = await _dbContext.Tenants
                    .Include(t => t.SsoAutoJoinRole)
                    .Include(t => t.ParentTenant)
                    .FirstOrDefaultAsync(t => t.Id == tenantId);

                if (tenant == null)
                {
                    return new ApiResponse<TenantDetailDto>(
                        default!,
                        false,
                        "Tenant not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound);
                }

                if (tenant.TenantType != TenantType.Enterprise)
                {
                    return new ApiResponse<TenantDetailDto>(
                        default!,
                        false,
                        "SSO settings only available for enterprise tenants",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed);
                }

                tenant.SsoAutoJoinDomains = dto.SsoAutoJoinDomains;
                tenant.SsoAutoJoinRoleId = dto.SsoAutoJoinRoleId;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Updated SSO settings for tenant {tenantId}: Domains={string.Join(",", dto.SsoAutoJoinDomains ?? new List<string>())}, RoleId={dto.SsoAutoJoinRoleId}");

                var tenantDto = _mapper.Map<TenantDetailDto>(tenant);
                return new ApiResponse<TenantDetailDto>(tenantDto, true, "SSO settings updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error configuring SSO settings for tenant {tenantId}: {ex.Message}", ex);
                return new ApiResponse<TenantDetailDto>(
                    default!,
                    false,
                    "Failed to update SSO settings",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError);
            }
        }
    }
}
