using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.enums;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.services
{
    internal class EnterpriseSignupService : IEnterpriseSignupService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IIdentityProviderAdminService _identityProviderAdminService;
        private readonly IAuthUrlBuilderService _authUrlBuilder;
        private readonly ILoggingService _logger;

        public EnterpriseSignupService(
            ApplicationDbContext dbContext,
            IIdentityProviderAdminService identityProviderAdminService,
            IAuthUrlBuilderService authUrlBuilder,
            ILoggingService logger)
        {
            _dbContext = dbContext;
            _identityProviderAdminService = identityProviderAdminService;
            _authUrlBuilder = authUrlBuilder;
            _logger = logger;
        }

        public async Task<ApiResponse<EnterpriseSignupResponseDto>> SignupAsync(EnterpriseSignupRequestDto request, string redirectUri)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation($"Enterprise signup request for company: {request.CompanyName}");

                    if (string.IsNullOrWhiteSpace(request.CompanyName))
                    {
                        return new ApiResponse<EnterpriseSignupResponseDto>(
                            default!,
                            false,
                            "Company name is required",
                            new List<string> { "CompanyName cannot be empty" },
                            StatusCodes.Status400BadRequest,
                            ErrorCodes.ValidationFailed);
                    }

                    if (string.IsNullOrWhiteSpace(request.ContactEmail))
                    {
                        return new ApiResponse<EnterpriseSignupResponseDto>(
                            default!,
                            false,
                            "Contact email is required",
                            new List<string> { "ContactEmail cannot be empty" },
                            StatusCodes.Status400BadRequest,
                            ErrorCodes.ValidationFailed);
                    }

                    // Generate unique realm name (preserve existing behavior)
                    var slug = !string.IsNullOrWhiteSpace(request.RequestedSubdomain)
                        ? request.RequestedSubdomain
                        : request.CompanyName.ToLowerInvariant()
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Replace("-", "");

                    var shortGuid = Guid.NewGuid().ToString("N").Substring(0, 4);
                    var realmName = $"tenant_{slug}_{shortGuid}";

                    _logger.LogInformation($"Generated realm name: {realmName}");

                    var realmConfig = new CreateRealmDto
                    {
                        Realm = realmName,
                        DisplayName = request.CompanyName,
                        Enabled = true,
                        RegistrationAllowed = true,
                        RegistrationEmailAsUsername = false,
                        LoginWithEmailAllowed = true,
                        VerifyEmail = true,
                        ResetPasswordAllowed = true,
                        EditUsernameAllowed = false,
                        RememberMe = true
                    };

                    var realmResult = await _identityProviderAdminService.CreateRealmWithClientAsync(realmConfig, request.CustomDomain);
                    if (!realmResult.Success)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError($"Failed to create Keycloak realm: {realmResult.Message}");

                        return new ApiResponse<EnterpriseSignupResponseDto>(
                            default!,
                            false,
                            realmResult.Message,
                            realmResult.Errors,
                            realmResult.StatusCode,
                            realmResult.ErrorCode);
                    }

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

                    var registrationUrl = await _authUrlBuilder.BuildEnterpriseFirstAdminRegistrationUrlAsync(realmName, redirectUri);

                    if (registrationUrl.StartsWith("ERROR:"))
                    {
                        // Tenant/realm already created; caller can decide if they want cleanup.
                        var errorMessage = registrationUrl.Substring(6);
                        return new ApiResponse<EnterpriseSignupResponseDto>(
                            default!,
                            false,
                            errorMessage,
                            new List<string> { errorMessage },
                            StatusCodes.Status500InternalServerError,
                            "CONFIG_ERROR");
                    }

                    var responseDto = new EnterpriseSignupResponseDto
                    {
                        TenantId = tenant.Id,
                        TenantName = tenant.Name,
                        RealmName = realmName,
                        CustomDomain = request.CustomDomain,
                        InvitationToken = string.Empty,
                        InvitationUrl = registrationUrl,
                        EmailSent = false,
                        Message = $"Enterprise tenant created. Please register at: {registrationUrl}"
                    };

                    return new ApiResponse<EnterpriseSignupResponseDto>(
                        responseDto,
                        true,
                        "Enterprise tenant created successfully",
                        null,
                        StatusCodes.Status200OK);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Error creating enterprise tenant: {ex.Message}", ex);
                    return new ApiResponse<EnterpriseSignupResponseDto>(
                        default!,
                        false,
                        "Error creating enterprise tenant",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError);
                }
            });
        }
    }
}
