using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/user-roles")]
    [ApiController]
    [Authorize]
    public class UserRolesController : ControllerBase
    {
        private readonly IKeycloakAdminService _keycloakAdminService;
        private readonly ILoggingService _logger;

        public UserRolesController(IKeycloakAdminService keycloakAdminService, ILoggingService logger)
        {
            _keycloakAdminService = keycloakAdminService;
            _logger = logger;
        }

        // GET: api/user-roles/{userId}
        [HttpGet("{userId}")]
        public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetUserRoles(string userId)
        {
            try
            {
                var roles = await _keycloakAdminService.GetUserRolesAsync(userId);
                return Ok(new ApiResponse<List<RoleDto>>(roles));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving roles for user {userId}: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<List<RoleDto>>(
                        new List<RoleDto>(),
                        false,
                        $"Failed to retrieve roles for user",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // POST: api/user-roles
        [HttpPost]
        public async Task<ActionResult<ApiResponse<bool>>> AssignRoleToUser([FromBody] UserRoleAssignmentDto assignDto)
        {
            try
            {
                var result = await _keycloakAdminService.AssignRoleToUserAsync(assignDto.UserId, assignDto.RoleName);
                return Ok(new ApiResponse<bool>(result, result, result ? "Role assigned successfully" : "Failed to assign role"));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error assigning role to user: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<bool>(
                        false,
                        false,
                        "Failed to assign role to user",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // POST: api/user-roles/bulk
        [HttpPost("bulk")]
        public async Task<ActionResult<ApiResponse<bool>>> AssignMultipleRolesToUser([FromBody] UserRolesBulkAssignmentDto bulkAssignDto)
        {
            try
            {
                var result = await _keycloakAdminService.AssignRolesToUserAsync(bulkAssignDto.UserId, bulkAssignDto.RoleNames);
                return Ok(new ApiResponse<bool>(result, result, result ? "Roles assigned successfully" : "Failed to assign roles"));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error bulk assigning roles to user: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<bool>(
                        false,
                        false,
                        "Failed to bulk assign roles to user",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // DELETE: api/user-roles/{userId}/{roleName}
        [HttpDelete("{userId}/{roleName}")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveRoleFromUser(string userId, string roleName)
        {
            try
            {
                var result = await _keycloakAdminService.RemoveRoleFromUserAsync(userId, roleName);
                return Ok(new ApiResponse<bool>(result, result, result ? "Role removed successfully" : "Failed to remove role"));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error removing role from user: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<bool>(
                        false,
                        false,
                        "Failed to remove role from user",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }
    }
}