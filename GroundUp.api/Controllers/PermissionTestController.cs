// Create a new file: GroundUp.api/Controllers/PermissionTestController.cs
using GroundUp.core.dtos;
using GroundUp.core.security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GroundUp.api.Controllers
{
    [Route("api/permission-test")]
    [ApiController]
    [Authorize] // Require authentication for all methods
    public class PermissionTestController : ControllerBase
    {
        // No permission required (just authentication)
        [HttpGet("public")]
        public IActionResult PublicEndpoint()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            return Ok(new ApiResponse<object>(new
            {
                message = "This endpoint only requires authentication",
                userId,
                roles
            }));
        }

        // Requires a specific permission
        [HttpGet("protected")]
        [RequiresPermission("test.access")]
        public IActionResult ProtectedEndpoint()
        {
            return Ok(new ApiResponse<string>("You have permission to access this endpoint!"));
        }

        // Requires a specific role
        [HttpGet("admin-only")]
        [RequiresPermission("ADMIN")]
        public IActionResult AdminOnlyEndpoint()
        {
            return Ok(new ApiResponse<string>("You are an admin!"));
        }

        // Requires multiple permissions (all)
        [HttpGet("multiple-permissions")]
        [RequiresPermission(new[] { "test.create", "test.read" }, requireAll: true)]
        public IActionResult MultiplePermissionsEndpoint()
        {
            return Ok(new ApiResponse<string>("You have all required permissions!"));
        }
    }
}