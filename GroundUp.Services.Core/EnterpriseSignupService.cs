using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Http;

namespace GroundUp.Services.Core;

internal sealed class EnterpriseSignupService : IEnterpriseSignupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityProviderAdminService _identityProviderAdminService;
    private readonly IAuthUrlBuilderService _authUrlBuilderService;
    private readonly IEnterpriseTenantProvisioningRepository _provisioningRepository;
    private readonly ILoggingService _logger;

    public EnterpriseSignupService(
        IUnitOfWork unitOfWork,
        IIdentityProviderAdminService identityProviderAdminService,
        IAuthUrlBuilderService authUrlBuilderService,
        IEnterpriseTenantProvisioningRepository provisioningRepository,
        ILoggingService logger)
    {
        _unitOfWork = unitOfWork;
        _identityProviderAdminService = identityProviderAdminService;
        _authUrlBuilderService = authUrlBuilderService;
        _provisioningRepository = provisioningRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<EnterpriseSignupResponseDto>> SignupAsync(EnterpriseSignupRequestDto request, string redirectUri)
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
            _logger.LogError($"Failed to create Keycloak realm: {realmResult.Message}");

            return new ApiResponse<EnterpriseSignupResponseDto>(
                default!,
                false,
                realmResult.Message,
                realmResult.Errors,
                realmResult.StatusCode,
                realmResult.ErrorCode);
        }

        ApiResponse<int> createTenantResult;
        try
        {
            createTenantResult = await _unitOfWork.ExecuteInTransactionAsync(
                ct => _provisioningRepository.CreateEnterpriseTenantAsync(request, realmName, ct),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating enterprise tenant: {ex.Message}", ex);
            return new ApiResponse<EnterpriseSignupResponseDto>(
                default!,
                false,
                "Error creating enterprise tenant",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError);
        }

        if (!createTenantResult.Success)
        {
            return new ApiResponse<EnterpriseSignupResponseDto>(
                default!,
                false,
                createTenantResult.Message,
                createTenantResult.Errors,
                createTenantResult.StatusCode,
                createTenantResult.ErrorCode);
        }

        _logger.LogInformation($"Created enterprise tenant: {request.CompanyName} (ID: {createTenantResult.Data})");

        var registrationUrl = await _authUrlBuilderService.BuildEnterpriseFirstAdminRegistrationUrlAsync(realmName, redirectUri);

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
            TenantId = createTenantResult.Data,
            TenantName = request.CompanyName,
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
}
