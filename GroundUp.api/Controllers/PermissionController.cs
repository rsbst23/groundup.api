using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GroundUp.Api.Controllers
{
    [Route("api/permissions")]
    [ApiController]
    [Authorize]
    public class PermissionController : ControllerBase
    {
        private readonly IPermissionAdminService _permissionService;
        private readonly IPermissionService _permissionQueryService;
        private readonly ILoggingService _logger;

        public PermissionController(
            IPermissionAdminService permissionService,
            IPermissionService permissionQueryService,
            ILoggingService logger)
        {
            _permissionService = permissionService;
            _permissionQueryService = permissionQueryService;
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
            var permissions = await _permissionQueryService.GetUserPermissions(userId);

            var result = new UserPermissionsDto
            {
                UserId = userId,
                Roles = roles,
                Permissions = permissions.ToList()
            };

            return Ok(new ApiResponse<UserPermissionsDto>(result));
        }

        // GET: api/permissions (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<PermissionDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _permissionService.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // GET: api/permissions/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<PermissionDto>>> GetById(int id)
        {
            var result = await _permissionService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // POST: api/permissions
        [HttpPost]
        public async Task<ActionResult<ApiResponse<PermissionDto>>> Create([FromBody] PermissionDto permissionDto)
        {
            if (permissionDto == null)
            {
                return BadRequest(new ApiResponse<PermissionDto>(
                    default!,
                    false,
                    "Invalid permission data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _permissionService.AddAsync(permissionDto);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // PUT: api/permissions/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<PermissionDto>>> Update(int id, [FromBody] PermissionDto permissionDto)
        {
            if (permissionDto == null)
            {
                return BadRequest(new ApiResponse<PermissionDto>(
                    default!,
                    false,
                    "Invalid permission data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            if (id != permissionDto.Id)
            {
                return BadRequest(new ApiResponse<PermissionDto>(
                    default!,
                    false,
                    "ID mismatch.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.IdMismatch
                ));
            }

            var result = await _permissionService.UpdateAsync(id, permissionDto);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // DELETE: api/permissions/{id}
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _permissionService.DeleteAsync(id);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        private static ApiResponse<T> ToApiResponse<T>(OperationResult<T> result)
            => new(
                result.Data!,
                result.Success,
                result.Message,
                result.Errors,
                result.StatusCode,
                result.ErrorCode
            );
    }
}