using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.Api.Controllers
{
    [Route("api/roles")]
    [ApiController]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        // GET: api/roles (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<RoleDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _roleService.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/roles/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<RoleDto>>> GetById(int id)
        {
            var result = await _roleService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/roles
        [HttpPost]
        public async Task<ActionResult<ApiResponse<RoleDto>>> Create([FromBody] RoleDto roleDto)
        {
            if (roleDto == null)
            {
                return BadRequest(new ApiResponse<RoleDto>(
                    default!,
                    false,
                    "Invalid role data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _roleService.AddAsync(roleDto);
            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/roles/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<RoleDto>>> Update(int id, [FromBody] RoleDto roleDto)
        {
            if (roleDto == null)
            {
                return BadRequest(new ApiResponse<RoleDto>(
                    default!,
                    false,
                    "Invalid role data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            if (id != roleDto.Id)
            {
                return BadRequest(new ApiResponse<RoleDto>(
                    default!,
                    false,
                    "ID mismatch.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.IdMismatch
                ));
            }

            var result = await _roleService.UpdateAsync(id, roleDto);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/roles/{id}
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _roleService.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
