using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Http;

namespace GroundUp.infrastructure.services
{
    internal class JoinLinkService : IJoinLinkService
    {
        private readonly ITenantJoinLinkRepository _joinLinkRepository;
        private readonly IAuthUrlBuilderService _authUrlBuilder;
        private readonly ILoggingService _logger;

        public JoinLinkService(
            ITenantJoinLinkRepository joinLinkRepository,
            IAuthUrlBuilderService authUrlBuilder,
            ILoggingService logger)
        {
            _joinLinkRepository = joinLinkRepository;
            _authUrlBuilder = authUrlBuilder;
            _logger = logger;
        }

        public async Task<ApiResponse<AuthUrlResponseDto>> BuildJoinAuthUrlAsync(string joinToken, string redirectUri)
        {
            try
            {
                _logger.LogInformation($"Processing join-link redirect for token: {joinToken}");

                if (string.IsNullOrWhiteSpace(joinToken))
                {
                    return new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        "Invalid join link",
                        new List<string> { "Join token is required" },
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed);
                }

                var joinLinkResult = await _joinLinkRepository.GetByTokenAsync(joinToken);
                if (!joinLinkResult.Success || joinLinkResult.Data == null)
                {
                    _logger.LogWarning($"Invalid join link token: {joinToken}");
                    return new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        "Invalid join link",
                        new List<string> { "Join link not found" },
                        StatusCodes.Status400BadRequest,
                        "INVALID_JOIN_LINK");
                }

                var joinLink = joinLinkResult.Data;

                if (joinLink.IsRevoked)
                {
                    _logger.LogWarning($"Join link {joinToken} has been revoked");
                    return new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        "Join link has been revoked",
                        null,
                        StatusCodes.Status400BadRequest,
                        "JOIN_LINK_REVOKED");
                }

                if (joinLink.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning($"Join link {joinToken} has expired");
                    return new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        "Join link has expired",
                        null,
                        StatusCodes.Status400BadRequest,
                        "JOIN_LINK_EXPIRED");
                }

                var realm = string.IsNullOrWhiteSpace(joinLink.RealmName)
                    ? "groundup"
                    : joinLink.RealmName;

                var authUrl = await _authUrlBuilder.BuildJoinLinkLoginUrlAsync(realm, joinToken, redirectUri);
                if (authUrl.StartsWith("ERROR:"))
                {
                    var errorMessage = authUrl.Substring(6);
                    return new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        errorMessage,
                        new List<string> { errorMessage },
                        StatusCodes.Status500InternalServerError,
                        "CONFIG_ERROR");
                }

                return new ApiResponse<AuthUrlResponseDto>(
                    new AuthUrlResponseDto { AuthUrl = authUrl, Action = "join_link" },
                    true,
                    "Join link login URL generated successfully",
                    null,
                    StatusCodes.Status200OK);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing join link redirect: {ex.Message}", ex);
                return new ApiResponse<AuthUrlResponseDto>(
                    null,
                    false,
                    "An error occurred processing the join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    "INTERNAL_ERROR");
            }
        }
    }
}
