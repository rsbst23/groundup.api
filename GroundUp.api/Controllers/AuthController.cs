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

        public AuthController(ILoggingService logger)
        {
            _logger = logger;
        }

        [HttpPost("logout")]
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

        // Optional: Debugging endpoint for development
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
    }
}