using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GroundUp.api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // REGISTER ENDPOINT
        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<string>>> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<string>(
                    string.Empty,
                    false,
                    "Invalid registration data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var user = new ApplicationUser
            {
                UserName = model.Username,  // Now using the username field
                Email = model.Email,
                FullName = model.FullName
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new ApiResponse<string>(
                    string.Empty,
                    false,
                    "User registration failed.",
                    result.Errors.Select(e => e.Description).ToList(),
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.RegistrationFailed
                ));
            }

            // Add user to default role (e.g., "User")
            var roleResult = await _userManager.AddToRoleAsync(user, "ADMIN");

            return StatusCode(StatusCodes.Status201Created, new ApiResponse<string>(
                "User registered successfully",
                true,
                "User created",
                null,
                StatusCodes.Status201Created
            ));
        }

        // LOGIN ENDPOINT
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<string>>> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<string>(
                    string.Empty,
                    false,
                    "Invalid login request.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            // First, try to find the user by username
            var user = await _userManager.FindByNameAsync(model.Identifier);

            // If not found by username, try by email
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(model.Identifier);
            }

            // If still not found or password is incorrect
            if (user == null || !(await _userManager.CheckPasswordAsync(user, model.Password)))
            {
                return Unauthorized(new ApiResponse<string>(
                    string.Empty,
                    false,
                    "Invalid username, email or password.",
                    null,
                    StatusCodes.Status401Unauthorized,
                    ErrorCodes.InvalidCredentials
                ));
            }

            var token = GenerateJwtToken(user);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,  // Prevents JavaScript access
                Secure = true,   // Requires HTTPS
                SameSite = SameSiteMode.None, // Prevents CSRF attacks
                Expires = DateTime.UtcNow.AddHours(2)
            };

            Response.Cookies.Append("AuthToken", token, cookieOptions);

            return Ok(new ApiResponse<string>(
                "Login successful.",
                true,
                "Authenticated successfully.",
                null,
                StatusCodes.Status200OK
            ));
        }

        // LOGOUT ENDPOINT - Clears the JWT Cookie
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("AuthToken");

            return Ok(new ApiResponse<string>(
                "Logged out successfully.",
                true,
                "You have been logged out.",
                null,
                StatusCodes.Status200OK
            ));
        }

        // GET USER PROFILE - Retrieves authenticated user's details
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetUserProfile()
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

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse<UserProfileDto>(
                    default!,
                    false,
                    "User not found.",
                    null,
                    StatusCodes.Status404NotFound,
                    ErrorCodes.UserNotFound
                ));
            }

            var roles = await _userManager.GetRolesAsync(user);

            var userProfile = new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email!,
                Username = user.UserName!,
                FullName = user.FullName,
                Roles = roles.ToList()
            };

            return Ok(new ApiResponse<UserProfileDto>(userProfile));
        }

        // GENERATE JWT TOKEN
        private string GenerateJwtToken(ApplicationUser user)
        {
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("JWT_SECRET_KEY is not configured in environment variables.");
            }

            var key = Encoding.UTF8.GetBytes(secretKey);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
