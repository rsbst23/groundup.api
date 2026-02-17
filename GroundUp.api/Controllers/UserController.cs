using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.Api.Controllers
{
    /// <summary>
    /// Controller for user management
    /// 
    /// SCOPE: Read-only user queries from local database
    /// 
    /// What this controller DOES:
    /// - Get list of users (paginated, tenant-filtered)
    /// - Get user details by ID (synced from Keycloak)
    /// 
    /// What this controller DOES NOT do (handled by Keycloak Admin UI):
    /// - Create users (users created via OAuth flows or Keycloak Admin UI)
    /// - Update user profiles (managed in Keycloak Account Console)
    /// - Delete users (managed in Keycloak Admin UI)
    /// - Enable/disable users (managed in Keycloak Admin UI)
    /// - Reset passwords (managed in Keycloak Account Console)
    /// - Assign Keycloak roles (SYSTEMADMIN, TENANTADMIN managed in Keycloak Admin UI)
    /// 
    /// Note: Tenant assignment is managed via:
    /// - TenantInvitationController (invite users to tenants)
    /// - UserTenantRepository (internal - assigns users to tenants after accepting invitation)
    /// </summary>
    [Route("api/users")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        #region User Query Operations

        /// <summary>
        /// Get all users (paginated, tenant-filtered)
        /// Queries local database for fast results
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<UserSummaryDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _userService.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get user details by ID
        /// Fetches fresh data from Keycloak and syncs to local database
        /// </summary>
        [HttpGet("{userId}")]
        public async Task<ActionResult<ApiResponse<UserDetailsDto>>> GetById(string userId)
        {
            var result = await _userService.GetByIdAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        #endregion
    }
}
