using GroundUp.core;
using GroundUp.core.dtos;
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

        public TenantController(
            ITenantRepository tenantRepository,
            ILoggingService logger,
            IIdentityProviderAdminService identityProviderAdminService,
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            IMapper mapper)
        {
            _tenantRepository = tenantRepository;
            _logger = logger;
            _identityProviderAdminService = identityProviderAdminService;
            _dbContext = dbContext;
            _configuration = configuration;
            _mapper = mapper;
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
        public async Task<ActionResult<ApiResponse<PaginatedData<TenantDto>>>> Get([FromQuery] FilterParams filterParams)
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
        public async Task<ActionResult<ApiResponse<TenantDto>>> GetById(int id)
        {
            var result = await _tenantRepository.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Create a new tenant
        /// For enterprise tenants, also creates a dedicated Keycloak realm
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TenantDto>>> Create([FromBody] CreateTenantDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ApiResponse<TenantDto>(
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
        public async Task<ActionResult<ApiResponse<TenantDto>>> Update(int id, [FromBody] UpdateTenantDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ApiResponse<TenantDto>(
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
        /// Enterprise tenant signup
        /// Creates new Keycloak realm and tenant, returns direct Keycloak registration URL
        /// First admin registers directly in Keycloak (similar to standard tenants)
        /// PUBLIC ENDPOINT - No authentication required (called during signup)
        /// </summary>
        [HttpPost("enterprise/signup")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<EnterpriseSignupResponseDto>>> EnterpriseSignup(
            [FromBody] EnterpriseSignupRequestDto request)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation($"Enterprise signup request for company: {request.CompanyName}");
                    
                    // 1. Validate request
                    if (string.IsNullOrWhiteSpace(request.CompanyName))
                    {
                        return BadRequest(new ApiResponse<EnterpriseSignupResponseDto>(
                            default!,
                            false,
                            "Company name is required",
                            new List<string> { "CompanyName cannot be empty" },
                            StatusCodes.Status400BadRequest,
                            ErrorCodes.ValidationFailed
                        ));
                    }
                    
                    if (string.IsNullOrWhiteSpace(request.ContactEmail))
                    {
                        return BadRequest(new ApiResponse<EnterpriseSignupResponseDto>(
                            default!,
                            false,
                            "Contact email is required",
                            new List<string> { "ContactEmail cannot be empty" },
                            StatusCodes.Status400BadRequest,
                            ErrorCodes.ValidationFailed
                        ));
                    }
                    
                    // 2. Generate unique realm name
                    var slug = request.RequestedSubdomain ?? 
                               request.CompanyName.ToLowerInvariant()
                                   .Replace(" ", "")
                                   .Replace(".", "")
                                   .Replace("-", "");
                    
                    var shortGuid = Guid.NewGuid().ToString("N").Substring(0, 4);
                    var realmName = $"tenant_{slug}_{shortGuid}";
                    
                    _logger.LogInformation($"Generated realm name: {realmName}");
                    
                    // 3. Create Keycloak realm with client configured for tenant's custom domain
                    // IMPORTANT: Registration is ENABLED for first user
                    var realmConfig = new CreateRealmDto
                    {
                        Realm = realmName,
                        DisplayName = request.CompanyName,
                        Enabled = true,
                        RegistrationAllowed = true,  // ? Enable registration for first user
                        RegistrationEmailAsUsername = false,
                        LoginWithEmailAllowed = true,
                        VerifyEmail = true,
                        ResetPasswordAllowed = true,
                        EditUsernameAllowed = false,
                        RememberMe = true
                    };
                    
                    var realmResult = await _identityProviderAdminService.CreateRealmWithClientAsync(
                        realmConfig,
                        request.CustomDomain);
                    
                    if (!realmResult.Success)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError($"Failed to create Keycloak realm: {realmResult.Message}");
                        return StatusCode(realmResult.StatusCode, new ApiResponse<EnterpriseSignupResponseDto>(
                            default!,
                            false,
                            realmResult.Message,
                            realmResult.Errors,
                            realmResult.StatusCode,
                            realmResult.ErrorCode
                        ));
                    }
                    
                    // 4. Create Tenant record
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
                    await _dbContext.SaveChangesAsync();
                    
                    await transaction.CommitAsync();
                    
                    _logger.LogInformation($"Created enterprise tenant: {tenant.Name} (ID: {tenant.Id})");
                    
                    // 5. Build direct Keycloak registration URL
                    var keycloakAuthUrl = Environment.GetEnvironmentVariable("KEYCLOAK_AUTH_SERVER_URL") 
                        ?? _configuration["Keycloak:AuthServerUrl"];
                    var clientId = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE") 
                        ?? _configuration["Keycloak:Resource"];
                    var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
                    
                    // Build state for callback
                    var state = new AuthCallbackState
                    {
                        Flow = "enterprise_first_admin",  // Explicit flow for enterprise first user
                        Realm = realmName
                    };
                    
                    var stateJson = JsonSerializer.Serialize(state);
                    var stateEncoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(stateJson));
                    
                    // Direct Keycloak registration URL
                    var registrationUrl = $"{keycloakAuthUrl}/realms/{realmName}/protocol/openid-connect/registrations" +
                                        $"?client_id={Uri.EscapeDataString(clientId)}" +
                                        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                                        $"&response_type=code" +
                                        $"&scope=openid%20email%20profile" +
                                        $"&state={Uri.EscapeDataString(stateEncoded)}";
                    
                    _logger.LogInformation($"Enterprise tenant created successfully. Registration URL: {registrationUrl}");
                    
                    var response = new ApiResponse<EnterpriseSignupResponseDto>(
                        new EnterpriseSignupResponseDto
                        {
                            TenantId = tenant.Id,
                            TenantName = tenant.Name,
                            RealmName = realmName,
                            CustomDomain = request.CustomDomain,
                            InvitationToken = "",  // Not used for first admin
                            InvitationUrl = registrationUrl,  // Direct Keycloak URL
                            EmailSent = false,  // No email sent
                            Message = $"Enterprise tenant created. Please register at: {registrationUrl}"
                        },
                        true,
                        "Enterprise tenant created successfully"
                    );
                    
                    return Ok(response);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Error creating enterprise tenant: {ex.Message}", ex);
                    return StatusCode(500, new ApiResponse<EnterpriseSignupResponseDto>(
                        default!,
                        false,
                        "Error creating enterprise tenant",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
                }
            });
        }

        /// <summary>
        /// Configure SSO auto-join settings for enterprise tenant
        /// POST /api/tenants/{id}/sso-settings
        /// </summary>
        [HttpPost("{id}/sso-settings")]
        public async Task<ActionResult<ApiResponse<TenantDto>>> ConfigureSsoSettings(
            int id,
            [FromBody] ConfigureSsoSettingsDto dto)
        {
            try
            {
                var tenant = await _dbContext.Tenants
                    .Include(t => t.SsoAutoJoinRole)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (tenant == null)
                {
                    return NotFound(new ApiResponse<TenantDto>(
                        default!,
                        false,
                        "Tenant not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    ));
                }

                if (tenant.TenantType != TenantType.Enterprise)
                {
                    return BadRequest(new ApiResponse<TenantDto>(
                        default!,
                        false,
                        "SSO settings only available for enterprise tenants",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    ));
                }

                // Update SSO settings
                tenant.SsoAutoJoinDomains = dto.SsoAutoJoinDomains;
                tenant.SsoAutoJoinRoleId = dto.SsoAutoJoinRoleId;

                await _dbContext.SaveChangesAsync();

                var tenantDto = _mapper.Map<TenantDto>(tenant);

                _logger.LogInformation($"Updated SSO settings for tenant {id}: Domains={string.Join(",", dto.SsoAutoJoinDomains ?? new List<string>())}, RoleId={dto.SsoAutoJoinRoleId}");

                return Ok(new ApiResponse<TenantDto>(
                    tenantDto,
                    true,
                    "SSO settings updated successfully"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error configuring SSO settings: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<TenantDto>(
                    default!,
                    false,
                    "Failed to update SSO settings",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                ));
            }
        }
    }
}
