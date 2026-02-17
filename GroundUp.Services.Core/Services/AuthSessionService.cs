using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.dtos.auth;
using GroundUp.Core.dtos.tenants;
using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Http;

namespace GroundUp.Services.Core.Services;

public sealed class AuthSessionService : IAuthSessionService
{
    private readonly ILoggingService _logger;
    private readonly IUserTenantRepository _userTenantRepository;
    private readonly ITokenService _tokenService;

    public AuthSessionService(
        ILoggingService logger,
        IUserTenantRepository userTenantRepository,
        ITokenService tokenService)
    {
        _logger = logger;
        _userTenantRepository = userTenantRepository;
        _tokenService = tokenService;
    }

    public async Task<ApiResponse<SetTenantResponseDto>> SetTenantAsync(
        Guid userId,
        SetTenantRequestDto request,
        IEnumerable<System.Security.Claims.Claim> existingClaims)
    {
        var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);

        if (request.TenantId == null)
        {
            if (userTenants.Count == 1)
            {
                var tenantId = userTenants[0].TenantId;
                var token = await _tokenService.GenerateTokenAsync(userId, tenantId, existingClaims);

                _logger.LogInformation("Tenant auto-selected (single membership). Token issued.");

                return new ApiResponse<SetTenantResponseDto>(
                    new SetTenantResponseDto
                    {
                        SelectionRequired = false,
                        AvailableTenants = null,
                        Token = token
                    },
                    true,
                    "Tenant set and token issued.",
                    null,
                    StatusCodes.Status200OK);
            }

            return new ApiResponse<SetTenantResponseDto>(
                new SetTenantResponseDto
                {
                    SelectionRequired = true,
                    AvailableTenants = userTenants.Select(ut => new TenantListItemDto
                    {
                        Id = ut.Tenant.Id,
                        Name = ut.Tenant.Name,
                        Description = ut.Tenant.Description
                    }).ToList(),
                    Token = null
                },
                true,
                "Tenant selection required.",
                null,
                StatusCodes.Status200OK);
        }

        var userTenant = await _userTenantRepository.GetUserTenantAsync(userId, request.TenantId.Value);
        if (userTenant == null)
        {
            _logger.LogWarning("User is not assigned to the selected tenant");
            return new ApiResponse<SetTenantResponseDto>(
                default!,
                false,
                "User is not assigned to the selected tenant.",
                null,
                StatusCodes.Status403Forbidden,
                ErrorCodes.Unauthorized);
        }

        var updatedToken = await _tokenService.GenerateTokenAsync(userId, request.TenantId.Value, existingClaims);

        _logger.LogInformation("Tenant selected. Token issued.");

        return new ApiResponse<SetTenantResponseDto>(
            new SetTenantResponseDto
            {
                SelectionRequired = false,
                AvailableTenants = null,
                Token = updatedToken
            },
            true,
            "Tenant set and token issued.",
            null,
            StatusCodes.Status200OK);
    }

    public async Task<string?> TryRefreshAuthTokenAsync(Guid userId, int tenantId, IEnumerable<System.Security.Claims.Claim> existingClaims)
    {
        try
        {
            var userTenant = await _userTenantRepository.GetUserTenantAsync(userId, tenantId);
            if (userTenant == null)
            {
                _logger.LogWarning("Token refresh skipped: user is no longer assigned to tenant.");
                return null;
            }

            var newToken = await _tokenService.GenerateTokenAsync(userId, tenantId, existingClaims);
            return string.IsNullOrWhiteSpace(newToken) ? null : newToken;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Token refresh failed: {ex.Message}", ex);
            return null;
        }
    }
}
