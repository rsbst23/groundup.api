using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;

        public UserController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        #region Standard CRUD Operations

        // GET: api/users (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<UserSummaryDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _userRepository.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/users/{userId}
        [HttpGet("{userId}")]
        public async Task<ActionResult<ApiResponse<UserDetailsDto>>> GetById(string userId)
        {
            var result = await _userRepository.GetByIdAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/users
        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserDetailsDto>>> Create([FromBody] CreateUserDto userDto)
        {
            if (userDto == null)
            {
                return BadRequest(new ApiResponse<UserDetailsDto>(
                    default!,
                    false,
                    "Invalid user data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _userRepository.AddAsync(userDto);
            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/users/{userId}
        [HttpPut("{userId}")]
        public async Task<ActionResult<ApiResponse<UserDetailsDto>>> Update(string userId, [FromBody] UpdateUserDto userDto)
        {
            if (userDto == null)
            {
                return BadRequest(new ApiResponse<UserDetailsDto>(
                    default!,
                    false,
                    "Invalid user data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _userRepository.UpdateAsync(userId, userDto);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/users/{userId}
        [HttpDelete("{userId}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(string userId)
        {
            var result = await _userRepository.DeleteAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region System Role Management

        // GET: api/users/{userId}/system-roles
        [HttpGet("{userId}/system-roles")]
        public async Task<ActionResult<ApiResponse<List<SystemRoleDto>>>> GetSystemRoles(string userId)
        {
            var result = await _userRepository.GetUserSystemRolesAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/users/{userId}/system-roles/{roleName}
        [HttpPost("{userId}/system-roles/{roleName}")]
        public async Task<ActionResult<ApiResponse<bool>>> AssignSystemRole(string userId, string roleName)
        {
            var result = await _userRepository.AssignSystemRoleToUserAsync(userId, roleName);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/users/{userId}/system-roles/{roleName}
        [HttpDelete("{userId}/system-roles/{roleName}")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveSystemRole(string userId, string roleName)
        {
            var result = await _userRepository.RemoveSystemRoleFromUserAsync(userId, roleName);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region User Management Operations

        // PUT: api/users/{userId}/enable
        [HttpPut("{userId}/enable")]
        public async Task<ActionResult<ApiResponse<bool>>> EnableUser(string userId)
        {
            var result = await _userRepository.SetUserEnabledAsync(userId, true);
            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/users/{userId}/disable
        [HttpPut("{userId}/disable")]
        public async Task<ActionResult<ApiResponse<bool>>> DisableUser(string userId)
        {
            var result = await _userRepository.SetUserEnabledAsync(userId, false);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/users/{userId}/reset-password
        [HttpPost("{userId}/reset-password")]
        public async Task<ActionResult<ApiResponse<bool>>> SendPasswordReset(string userId)
        {
            var result = await _userRepository.SendPasswordResetEmailAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        #endregion
    }
}
