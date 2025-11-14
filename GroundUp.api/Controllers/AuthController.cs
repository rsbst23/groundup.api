using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GroundUp.api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ILoggingService _logger;
        private readonly IUserTenantRepository _userTenantRepository;
        private readonly ITokenService _tokenService;

        public AuthController(ILoggingService logger, IUserTenantRepository userTenantRepository, ITokenService tokenService)
        {
            _logger = logger;
            _userTenantRepository = userTenantRepository;
            _tokenService = tokenService;
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("AuthToken");
            _logger.LogInformation("User logged out - cookie cleared");
            return Ok(new ApiResponse<string>("Logged out successfully."));
        }

        [HttpGet("me")]
        [Authorize]
        public ActionResult<ApiResponse<UserProfileDto>> GetUserProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized(new ApiResponse<UserProfileDto>(
                    default!,
                    false,
                    "Unauthorized access.",
                    null,
                    StatusCodes.Status401Unauthorized,
                    ErrorCodes.Unauthorized
                ));
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
            return Ok(new ApiResponse<UserProfileDto>(userProfile));
        }

        [HttpGet("debug-token")]
        [Authorize]
        public ActionResult<ApiResponse<Dictionary<string, string>>> DebugToken()
        {
            var claims = User.Claims.ToDictionary(
                c => c.Type,
                c => c.Value
            );

            return Ok(new ApiResponse<Dictionary<string, string>>(claims));
        }

        [HttpPost("set-tenant")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<SetTenantResponseDto>>> SetTenant([FromBody] SetTenantRequestDto request)
        {
            ApiResponse<SetTenantResponseDto> response;

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                _logger.LogWarning("Failed to parse userId as Guid");
                response = new ApiResponse<SetTenantResponseDto>(default!, false, "Unauthorized access.", null, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized);
                return StatusCode(response.StatusCode, response);
            }
            
            var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);

            if (request.TenantId == null)
            {
                if (userTenants.Count == 1)
                {
                    var token = await _tokenService.GenerateTokenAsync(userId, userTenants[0].TenantId, User.Claims);
                    
                    Response.Cookies.Append("AuthToken", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddHours(1)
                    });
                    
                    _logger.LogInformation("Token generated and cookie set");
                    
                    response = new ApiResponse<SetTenantResponseDto>(
                        new SetTenantResponseDto {
                            SelectionRequired = false,
                            AvailableTenants = null,
                            Token = token
                        },
                        true,
                        "Tenant set and token issued.",
                        null,
                        StatusCodes.Status200OK
                    );
                }
                else
                {
                    response = new ApiResponse<SetTenantResponseDto>(
                        new SetTenantResponseDto {
                            SelectionRequired = true,
                            AvailableTenants = userTenants.Select(ut => ut.Tenant).ToList(),
                            Token = null
                        },
                        true,
                        "Tenant selection required.",
                        null,
                        StatusCodes.Status200OK
                    );
                }
            }
            else
            {
                var userTenant = await _userTenantRepository.GetUserTenantAsync(userId, request.TenantId.Value);
                if (userTenant == null)
                {
                    _logger.LogWarning("User is not assigned to the selected tenant");
                    response = new ApiResponse<SetTenantResponseDto>(
                        default!, false, "User is not assigned to the selected tenant.", null, StatusCodes.Status403Forbidden, ErrorCodes.Unauthorized);
                }
                else
                {
                    var token = await _tokenService.GenerateTokenAsync(userId, request.TenantId.Value, User.Claims);
                    
                    Response.Cookies.Append("AuthToken", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddHours(1)
                    });
                    
                    _logger.LogInformation("Token generated and cookie set");
                    
                    response = new ApiResponse<SetTenantResponseDto>(
                        new SetTenantResponseDto {
                            SelectionRequired = false,
                            AvailableTenants = null,
                            Token = token
                        },
                        true,
                        "Tenant set and token issued.",
                        null,
                        StatusCodes.Status200OK
                    );
                }
            }
            
            return StatusCode(response.StatusCode, response);
        }
    }
}