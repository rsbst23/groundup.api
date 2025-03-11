using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GroundUp.api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IKeycloakService _keycloakService;
        private readonly ILoggingService _logger;

        public AuthController(IKeycloakService keycloakService, ILoggingService logger)
        {
            _keycloakService = keycloakService;
            _logger = logger;
        }

        [HttpGet("test-keycloak-connection")]
        [AllowAnonymous]
        public async Task<IActionResult> TestKeycloakConnection()
        {
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync("http://host.docker.internal:8080/realms/groundup/.well-known/openid-configuration");
            var content = await response.Content.ReadAsStringAsync();

            return Ok(new
            {
                response.StatusCode,
                Content = content
            });
        }

        [HttpGet("token-check")]
        [AllowAnonymous]
        public IActionResult TokenCheck()
        {
            var token = Request.Headers["Authorization"].FirstOrDefault()?.Substring("Bearer ".Length);
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest("No token provided in Authorization header");
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                return Ok(new
                {
                    Message = "Token parsed",
                    Subject = jwtToken.Subject,
                    Issuer = jwtToken.Issuer,
                    ClaimsCount = jwtToken.Claims.Count()
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }

        [HttpGet("manual-token-test")]
        [AllowAnonymous]
        public IActionResult ManualTokenTest()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Missing or invalid Authorization header");
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            try
            {
                // Print out the token for debugging
                _logger.LogInformation($"Token: {token.Substring(0, 50)}...");

                // Try to manually parse and validate it 
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var claims = jwtToken.Claims.Select(c => new { c.Type, c.Value }).ToList();
                return Ok(new { Message = "Token received and parsed", TokenInfo = jwtToken.Header, Claims = claims });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error parsing token: {ex.Message}");
            }
        }

        [HttpGet("debug-auth")]
        public IActionResult DebugAuthentication()
        {
            // Log detailed authentication information
            _logger.LogInformation("Debug authentication endpoint accessed");

            // Attempt to extract and log token
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null)
            {
                _logger.LogInformation($"Authorization Header: {authHeader}");
            }
            else
            {
                _logger.LogWarning("No Authorization header found");
            }

            // Log all incoming headers
            foreach (var header in Request.Headers)
            {
                _logger.LogInformation($"Header - {header.Key}: {header.Value}");
            }

            return Ok(new
            {
                Message = "Authentication debug information",
                AuthHeaderPresent = authHeader != null
            });
        }

        [Authorize]
        [HttpGet("test-auth")]
        [Authorize(Roles = "ADMIN")] // Allow both ADMIN and USER roles
        public IActionResult TestAuth()
        {
            // Log all incoming headers
            foreach (var header in Request.Headers)
            {
                _logger.LogInformation($"Header - {header.Key}: {header.Value}");
            }

            // Explicitly try to extract token
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            _logger.LogInformation($"Authorization Header: {authHeader}");

            // Extract token from header
            var token = authHeader?.Substring("Bearer ".Length).Trim();
            _logger.LogInformation($"Extracted Token: {token}");

            // Existing authentication logic
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

            return Ok(new
            {
                Message = "Successfully authenticated",
                Username = User.Identity.Name,
                Claims = claims,
                IsAdmin = User.IsInRole("ADMIN"),
                IsUser = User.IsInRole("USER")
            });
        }

        // We don't need register/login endpoints anymore as Keycloak handles this

        // LOGOUT ENDPOINT - Clears the JWT Cookie
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("AuthToken");
            _logger.LogInformation("User logged out - cookie cleared");
            return Ok(new ApiResponse<string>("Logged out successfully."));
        }

        // GET USER PROFILE - Retrieves authenticated user's details from token
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

        // DEBUG ENDPOINT - Shows all claims in the token (helpful during development)
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