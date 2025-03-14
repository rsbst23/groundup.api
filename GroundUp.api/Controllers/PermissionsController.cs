// Create a new file: GroundUp.api/Controllers/PermissionsController.cs
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GroundUp.api.Controllers
{
    [Route("api/permissions")]
    [ApiController]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;
        private readonly ILoggingService _logger;

        public PermissionsController(IPermissionService permissionService, ILoggingService logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        // GET: api/permissions/me
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<UserPermissionsDto>>> GetMyPermissions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<UserPermissionsDto>(
                    new UserPermissionsDto(),
                    false,
                    "User ID not found",
                    null,
                    StatusCodes.Status401Unauthorized,
                    ErrorCodes.Unauthorized
                ));
            }

            // Get roles from claims
            var roles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            // Get permissions from service
            var permissions = await _permissionService.GetUserPermissions(userId);

            // Create result object
            var result = new UserPermissionsDto
            {
                UserId = userId,
                Roles = roles,
                Permissions = permissions.ToList()
            };

            return Ok(new ApiResponse<UserPermissionsDto>(result));
        }
    }
}