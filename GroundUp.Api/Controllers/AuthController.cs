using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.dtos.auth;
using GroundUp.Core.dtos.tenants;
using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GroundUp.Api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ILoggingService _logger;
        private readonly IAuthSessionService _authSessionService;
        private readonly IAuthFlowService _authFlowService;
        private readonly IAuthUrlBuilderService _authUrlBuilder;
        private readonly IEnterpriseSignupService _enterpriseSignupService;

        public AuthController(
            ILoggingService logger,
            IAuthSessionService authSessionService,
            IAuthFlowService authFlowService,
            IAuthUrlBuilderService authUrlBuilder,
            IEnterpriseSignupService enterpriseSignupService)
        {
            _logger = logger;
            _authSessionService = authSessionService;
            _authFlowService = authFlowService;
            _authUrlBuilder = authUrlBuilder;
            _enterpriseSignupService = enterpriseSignupService;
        }

        /// <summary>
        /// Handles OAuth callback - processes authentication and returns result as JSON
        /// React frontend is responsible for navigation based on the response
        /// </summary>
        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthCallbackResponseDto>>> AuthCallback(
            [FromQuery] string code,
            [FromQuery] string? state)
        {
            try
            {
                var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";

                var responseDto = await _authFlowService.HandleAuthCallbackAsync(
                    code,
                    state,
                    redirectUri);

                // Controller responsibility: set cookie only at the HTTP boundary.
                if (responseDto.Success && !string.IsNullOrWhiteSpace(responseDto.Token))
                {
                    SetAuthCookie(responseDto.Token);
                }

                var statusCode = responseDto.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest;
                var finalResponse = new ApiResponse<AuthCallbackResponseDto>(
                    responseDto,
                    responseDto.Success,
                    responseDto.Success ? "Authentication successful" : "Authentication failed",
                    responseDto.ErrorMessage != null ? new List<string> { responseDto.ErrorMessage } : null,
                    statusCode,
                    responseDto.Success ? null : "AUTH_FLOW_FAILED");

                return StatusCode(finalResponse.StatusCode, finalResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in auth callback: {ex.Message}", ex);
                var response = new ApiResponse<AuthCallbackResponseDto>(
                    default!,
                    false,
                    "An unexpected error occurred during authentication",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    "UNEXPECTED_ERROR");

                return StatusCode(response.StatusCode, response);
            }
        }

        private void SetAuthCookie(string token, string cookieName = "AuthToken")
        {
            Response.Cookies.Append(cookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            });
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("AuthToken");
            _logger.LogInformation("User logged out - cookie cleared");

            var response = new ApiResponse<string>(
                "Logged out successfully.",
                true,
                "User logged out successfully",
                null,
                StatusCodes.Status200OK
            );

            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Get login URL for authentication
        /// Returns the Keycloak login URL - client should redirect user to this URL
        /// </summary>
        [HttpGet("login")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthUrlResponseDto>>> GetLoginUrl(
            [FromQuery] string? domain = null,
            [FromQuery] string? returnUrl = null)
        {
            try
            {
                var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
                var authUrl = await _authUrlBuilder.BuildLoginUrlAsync(domain, redirectUri, returnUrl);

                if (authUrl.StartsWith("ERROR:"))
                {
                    var errorMessage = authUrl.Substring(6);
                    return BadRequest(new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        errorMessage,
                        new List<string> { errorMessage },
                        StatusCodes.Status400BadRequest,
                        "INVALID_DOMAIN"
                    ));
                }

                var response = new ApiResponse<AuthUrlResponseDto>(
                    new AuthUrlResponseDto { AuthUrl = authUrl, Action = "login" },
                    true,
                    "Login URL generated successfully",
                    null,
                    StatusCodes.Status200OK
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating login URL: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<AuthUrlResponseDto>(
                    null,
                    false,
                    "Failed to generate login URL",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    "LOGIN_URL_GENERATION_FAILED"
                ));
            }
        }

        /// <summary>
        /// Get registration URL for new user signup (standard tenants only)
        /// Returns the Keycloak registration URL in shared realm
        /// Enterprise tenants get registration URL directly from enterprise signup endpoint
        /// </summary>
        [HttpGet("register")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthUrlResponseDto>>> GetRegisterUrl(
            [FromQuery] string? returnUrl = null)
        {
            try
            {
                var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
                var authUrl = await _authUrlBuilder.BuildRegistrationUrlAsync(redirectUri, returnUrl);

                if (authUrl.StartsWith("ERROR:"))
                {
                    var errorMessage = authUrl.Substring(6);
                    return BadRequest(new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        errorMessage,
                        new List<string> { errorMessage },
                        StatusCodes.Status400BadRequest,
                        "INVALID_DOMAIN"
                    ));
                }

                var response = new ApiResponse<AuthUrlResponseDto>(
                    new AuthUrlResponseDto { AuthUrl = authUrl, Action = "register" },
                    true,
                    "Registration URL generated successfully",
                    null,
                    StatusCodes.Status200OK
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating registration URL: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<AuthUrlResponseDto>(
                    null,
                    false,
                    "Failed to generate registration URL",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    "REGISTRATION_URL_GENERATION_FAILED"
                ));
            }
        }

        [HttpGet("me")]
        [Authorize]
        public ActionResult<ApiResponse<UserProfileDto>> GetUserProfile()
        {
            var userId = User.FindFirstValue("ApplicationUserId");
            if (userId == null)
            {
                var response = new ApiResponse<UserProfileDto>(
                    default!,
                    false,
                    "Unauthorized access.",
                    null,
                    StatusCodes.Status401Unauthorized,
                    ErrorCodes.Unauthorized
                );
                return StatusCode(response.StatusCode, response);
            }

            var userProfile = new UserProfileDto
            {
                Id = userId,
                Email = User.FindFirstValue(ClaimTypes.Email) ?? "",
                Username = User.FindFirstValue("preferred_username") ??
                           User.FindFirstValue(ClaimTypes.Name) ?? "",
                FullName = User.FindFirstValue("name") ?? "",
                Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
            };

            _logger.LogInformation($"User profile retrieved for {userProfile.Username}");

            var successResponse = new ApiResponse<UserProfileDto>(
                userProfile,
                true,
                "User profile retrieved successfully",
                null,
                StatusCodes.Status200OK
            );

            return StatusCode(successResponse.StatusCode, successResponse);
        }

        [HttpPost("set-tenant")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<SetTenantResponseDto>>> SetTenant([FromBody] SetTenantRequestDto request)
        {
            var userIdStr = User.FindFirstValue("ApplicationUserId");

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                _logger.LogWarning("Failed to parse userId as Guid");
                var unauthorized = new ApiResponse<SetTenantResponseDto>(default!, false, "Unauthorized access.", null, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized);
                return StatusCode(unauthorized.StatusCode, unauthorized);
            }

            var response = await _authSessionService.SetTenantAsync(userId, request, User.Claims);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Data?.Token))
            {
                Response.Cookies.Append("AuthToken", response.Data.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(1)
                });
            }

            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Enterprise tenant signup (onboarding)
        /// Creates new Keycloak realm + tenant and returns the direct Keycloak registration URL for the first admin.
        /// PUBLIC ENDPOINT - no authentication required.
        /// </summary>
        [HttpPost("enterprise/signup")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<EnterpriseSignupResponseDto>>> EnterpriseSignup(
            [FromBody] EnterpriseSignupRequestDto request)
        {
            try
            {
                var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
                var result = await _enterpriseSignupService.SignupAsync(request, redirectUri);
                return StatusCode(result.StatusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating enterprise tenant: {ex.Message}", ex);
                var response = new ApiResponse<EnterpriseSignupResponseDto>(
                    default!,
                    false,
                    "Error creating enterprise tenant",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError);
                return StatusCode(response.StatusCode, response);
            }
        }
    }
}