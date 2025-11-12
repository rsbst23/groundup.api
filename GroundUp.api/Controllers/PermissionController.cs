using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/permissions")]
    [ApiController]
    [Authorize]
    public class PermissionController : ControllerBase
    {
        private readonly IPermissionRepository _permissionRepository;

        public PermissionController(IPermissionRepository permissionRepository)
        {
            _permissionRepository = permissionRepository;
        }

        // GET: api/permissions (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<PermissionDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _permissionRepository.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/permissions/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<PermissionDto>>> GetById(int id)
        {
            var result = await _permissionRepository.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
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

            var result = await _permissionRepository.AddAsync(permissionDto);
            return StatusCode(result.StatusCode, result);
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

            var result = await _permissionRepository.UpdateAsync(id, permissionDto);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/permissions/{id}
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _permissionRepository.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}