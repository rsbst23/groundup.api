using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;
using GroundUp.core.entities;
using GroundUp.core.enums;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using AutoMapper;

namespace GroundUp.api.Controllers
{
    /// <summary>
    /// Controller for tenant management
    /// Authorization enforced via repository layer (RequiresPermission attributes on ITenantRepository)
    /// </summary>
    [Route("api/tenants")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly ILoggingService _logger;
        private readonly IIdentityProviderAdminService _identityProviderAdminService;
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly ITenantSsoSettingsService _tenantSsoSettingsService;

        public TenantController(
            ITenantRepository tenantRepository,
            ILoggingService logger,
            IIdentityProviderAdminService identityProviderAdminService,
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            IMapper mapper,
            ITenantSsoSettingsService tenantSsoSettingsService)
        {
            _tenantRepository = tenantRepository;
            _logger = logger;
            _identityProviderAdminService = identityProviderAdminService;
            _dbContext = dbContext;
            _configuration = configuration;
            _mapper = mapper;
            _tenantSsoSettingsService = tenantSsoSettingsService;
        }

        /// <summary>
        /// Resolve which Keycloak realm to use for a given URL
        /// PUBLIC ENDPOINT - No authentication required (called before user login)
        /// </summary>
        [HttpPost("resolve-realm")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<RealmResolutionResponseDto>>> ResolveRealm([FromBody] RealmResolutionRequestDto dto)
        {
            try
            {
                _logger.LogInformation($"Realm resolution requested for URL: {dto.Url}");
                
                var result = await _tenantRepository.ResolveRealmByUrlAsync(dto.Url);

                var response = new ApiResponse<RealmResolutionResponseDto>(
                    result.Data!,
                    result.Success,
                    result.Message,
                    result.Errors,
                    result.StatusCode,
                    result.ErrorCode
                );

                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error resolving realm for URL {dto.Url}: {ex.Message}", ex);
                
                // Return default realm on error to avoid blocking authentication
                var response = new ApiResponse<RealmResolutionResponseDto>(
                    new RealmResolutionResponseDto 
                    { 
                        Realm = "groundup", // TODO: Get from system settings
                        IsEnterprise = false 
                    },
                    false,
                    "Error resolving realm, using default",
                    new List<string> { ex.Message },
                    StatusCodes.Status200OK
                );
                
                return StatusCode(response.StatusCode, response);
            }
        }

        /// <summary>
        /// Get all tenants (paginated)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<TenantListItemDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _tenantRepository.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Export tenants to CSV or JSON
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] string format = "csv",
            [FromQuery] string? sortBy = null,
            [FromQuery] bool exportAll = true,
            [FromQuery] FilterParams? filterParams = null)
        {
            filterParams ??= new FilterParams();

            if (exportAll)
            {
                filterParams.PageSize = 10000;
                filterParams.PageNumber = 1;
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                filterParams.SortBy = sortBy;
            }

            var result = await _tenantRepository.ExportAsync(filterParams, format);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            string contentType;
            string filename;

            switch (format.ToLower())
            {
                case "json":
                    contentType = "application/json";
                    filename = $"tenants-{DateTime.Now:yyyy-MM-dd}.json";
                    break;
                case "csv":
                default:
                    contentType = "text/csv";
                    filename = $"tenants-{DateTime.Now:yyyy-MM-dd}.csv";
                    break;
            }

            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{filename}\"");
            return File(result.Data, contentType, filename);
        }

        /// <summary>
        /// Get tenant by ID
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<TenantDetailDto>>> GetById(int id)
        {
            var result = await _tenantRepository.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Create a new tenant
        /// For enterprise tenants, also creates a dedicated Keycloak realm
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TenantDetailDto>>> Create([FromBody] CreateTenantDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ApiResponse<TenantDetailDto>(
                    default!,
                    false,
                    "Invalid tenant data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _tenantRepository.AddAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Update an existing tenant
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<TenantDetailDto>>> Update(int id, [FromBody] UpdateTenantDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ApiResponse<TenantDetailDto>(
                    default!,
                    false,
                    "Invalid tenant data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _tenantRepository.UpdateAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Delete a tenant (only if no child tenants or users exist)
        /// For enterprise tenants, also deletes the Keycloak realm
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _tenantRepository.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Configure SSO auto-join settings for enterprise tenant
        /// POST /api/tenants/{id}/sso-settings
        /// </summary>
        [HttpPost("{id}/sso-settings")]
        public async Task<ActionResult<ApiResponse<TenantDetailDto>>> ConfigureSsoSettings(
            int id,
            [FromBody] ConfigureSsoSettingsDto dto)
        {
            var result = await _tenantSsoSettingsService.ConfigureSsoSettingsAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
