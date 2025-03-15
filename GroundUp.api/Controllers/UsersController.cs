using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IKeycloakAdminService _keycloakAdminService;
        private readonly ILoggingService _logger;

        public UsersController(IKeycloakAdminService keycloakAdminService, ILoggingService logger)
        {
            _keycloakAdminService = keycloakAdminService;
            _logger = logger;
        }

        // GET: api/users
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserSummaryDto>>>> GetAllUsers()
        {
            try
            {
                var users = await _keycloakAdminService.GetAllUsersAsync();
                return Ok(new ApiResponse<List<UserSummaryDto>>(users));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving users: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<List<UserSummaryDto>>(
                        new List<UserSummaryDto>(),
                        false,
                        "Failed to retrieve users from Keycloak",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // GET: api/users/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<UserDetailsDto>>> GetUserById(string id)
        {
            try
            {
                var user = await _keycloakAdminService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new ApiResponse<UserDetailsDto>(
                        default!,
                        false,
                        $"User with ID '{id}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    ));
                }

                return Ok(new ApiResponse<UserDetailsDto>(user));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving user with ID '{id}': {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<UserDetailsDto>(
                        default!,
                        false,
                        $"Failed to retrieve user with ID '{id}' from Keycloak",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // GET: api/users/by-username/{username}
        [HttpGet("by-username/{username}")]
        public async Task<ActionResult<ApiResponse<UserDetailsDto>>> GetUserByUsername(string username)
        {
            try
            {
                var user = await _keycloakAdminService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    return NotFound(new ApiResponse<UserDetailsDto>(
                        default!,
                        false,
                        $"User with username '{username}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    ));
                }

                return Ok(new ApiResponse<UserDetailsDto>(user));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving user with username '{username}': {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<UserDetailsDto>(
                        default!,
                        false,
                        $"Failed to retrieve user with username '{username}' from Keycloak",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }
    }
}