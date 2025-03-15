using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/role-permissions")]
    [ApiController]
    [Authorize]
    public class RolePermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;
        private readonly ILoggingService _logger;

        public RolePermissionsController(IPermissionService permissionService, ILoggingService logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        // GET: api/role-permissions
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<RolePermissionMappingDto>>>> GetAllRolePermissionMappings()
        {
            try
            {
                var mappings = await _permissionService.GetAllRolePermissionMappingsAsync();
                return Ok(mappings);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving role-permission mappings: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<List<RolePermissionMappingDto>>(
                        new List<RolePermissionMappingDto>(),
                        false,
                        "Failed to retrieve role-permission mappings",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // GET: api/role-permissions/{roleName}
        [HttpGet("{roleName}")]
        public async Task<ActionResult<ApiResponse<List<RolePermissionDto>>>> GetPermissionsByRole(string roleName)
        {
            try
            {
                var result = await _permissionService.GetRolePermissionsAsync(roleName);
                return StatusCode(result.StatusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving permissions for role '{roleName}': {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<List<RolePermissionDto>>(
                        new List<RolePermissionDto>(),
                        false,
                        $"Failed to retrieve permissions for role '{roleName}'",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // POST: api/role-permissions
        [HttpPost]
        public async Task<ActionResult<ApiResponse<RolePermissionDto>>> AssignPermissionToRole([FromBody] AssignPermissionDto assignDto)
        {
            try
            {
                var result = await _permissionService.AssignPermissionToRoleAsync(assignDto.RoleName, assignDto.PermissionId);
                return StatusCode(result.StatusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error assigning permission to role: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<RolePermissionDto>(
                        default!,
                        false,
                        "Failed to assign permission to role",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // POST: api/role-permissions/bulk
        [HttpPost("bulk")]
        public async Task<ActionResult<ApiResponse<bool>>> AssignMultiplePermissionsToRole([FromBody] BulkAssignPermissionsDto bulkAssignDto)
        {
            try
            {
                var result = await _permissionService.AssignMultiplePermissionsToRoleAsync(bulkAssignDto.RoleName, bulkAssignDto.PermissionIds);
                return StatusCode(result.StatusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error bulk assigning permissions to role: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<bool>(
                        false,
                        false,
                        "Failed to bulk assign permissions to role",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // DELETE: api/role-permissions/{roleName}/{permissionId}
        [HttpDelete("{roleName}/{permissionId}")]
        public async Task<ActionResult<ApiResponse<bool>>> RemovePermissionFromRole(string roleName, int permissionId)
        {
            try
            {
                var result = await _permissionService.RemovePermissionFromRoleAsync(roleName, permissionId);
                return StatusCode(result.StatusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error removing permission from role: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<bool>(
                        false,
                        false,
                        "Failed to remove permission from role",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }
    }
}