using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GroundUp.api.Controllers
{
    [EnableRateLimiting("AdminApiPolicy")]
    [Route("api/roles")]
    [ApiController]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly IKeycloakAdminService _keycloakAdminService;
        private readonly ILoggingService _logger;

        public RolesController(IKeycloakAdminService keycloakAdminService, ILoggingService logger)
        {
            _keycloakAdminService = keycloakAdminService;
            _logger = logger;
        }

        // GET: api/roles
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetAllRoles()
        {
            try
            {
                var roles = await _keycloakAdminService.GetAllRolesAsync();
                return Ok(new ApiResponse<List<RoleDto>>(roles));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving roles: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<List<RoleDto>>(
                        new List<RoleDto>(),
                        false,
                        "Failed to retrieve roles from Keycloak",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // GET: api/roles/{name}
        [HttpGet("{name}")]
        public async Task<ActionResult<ApiResponse<RoleDto>>> GetRoleByName(string name)
        {
            try
            {
                var role = await _keycloakAdminService.GetRoleByNameAsync(name);
                if (role == null)
                {
                    return NotFound(new ApiResponse<RoleDto>(
                        default!,
                        false,
                        $"Role '{name}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    ));
                }

                return Ok(new ApiResponse<RoleDto>(role));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving role '{name}': {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<RoleDto>(
                        default!,
                        false,
                        $"Failed to retrieve role '{name}' from Keycloak",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // POST: api/roles
        [HttpPost]
        public async Task<ActionResult<ApiResponse<RoleDto>>> CreateRole([FromBody] CreateRoleDto roleDto)
        {
            try
            {
                var role = await _keycloakAdminService.CreateRoleAsync(roleDto);
                return StatusCode(StatusCodes.Status201Created, new ApiResponse<RoleDto>(role, true, "Role created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating role: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<RoleDto>(
                        default!,
                        false,
                        "Failed to create role in Keycloak",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // PUT: api/roles/{name}
        [HttpPut("{name}")]
        public async Task<ActionResult<ApiResponse<RoleDto>>> UpdateRole(string name, [FromBody] UpdateRoleDto roleDto)
        {
            try
            {
                var updatedRole = await _keycloakAdminService.UpdateRoleAsync(name, roleDto);
                if (updatedRole == null)
                {
                    return NotFound(new ApiResponse<RoleDto>(
                        default!,
                        false,
                        $"Role '{name}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    ));
                }

                return Ok(new ApiResponse<RoleDto>(updatedRole));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating role '{name}': {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<RoleDto>(
                        default!,
                        false,
                        $"Failed to update role '{name}' in Keycloak",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // DELETE: api/roles/{name}
        [HttpDelete("{name}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteRole(string name)
        {
            try
            {
                var result = await _keycloakAdminService.DeleteRoleAsync(name);
                if (!result)
                {
                    return NotFound(new ApiResponse<bool>(
                        false,
                        false,
                        $"Role '{name}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    ));
                }

                return Ok(new ApiResponse<bool>(true, true, $"Role '{name}' deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting role '{name}': {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<bool>(
                        false,
                        false,
                        $"Failed to delete role '{name}' from Keycloak",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }
    }
}