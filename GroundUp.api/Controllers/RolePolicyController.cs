using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/role-policies")]
    [ApiController]
    public class RolePolicyController : ControllerBase
    {
        private readonly IRolePolicyRepository _rolePolicyRepository;

        public RolePolicyController(IRolePolicyRepository rolePolicyRepository)
        {
            _rolePolicyRepository = rolePolicyRepository;
        }

        // GET: api/role-policies (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<RolePolicyDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _rolePolicyRepository.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/role-policies/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<RolePolicyDto>>> GetById(int id)
        {
            var result = await _rolePolicyRepository.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/role-policies
        [HttpPost]
        public async Task<ActionResult<ApiResponse<RolePolicyDto>>> Create([FromBody] RolePolicyDto rolePolicyDto)
        {
            if (rolePolicyDto == null)
            {
                return BadRequest(new ApiResponse<RolePolicyDto>(
                    default!,
                    false,
                    "Invalid role policy data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _rolePolicyRepository.AddAsync(rolePolicyDto);
            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/role-policies/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<RolePolicyDto>>> Update(int id, [FromBody] RolePolicyDto rolePolicyDto)
        {
            if (rolePolicyDto == null)
            {
                return BadRequest(new ApiResponse<RolePolicyDto>(
                    default!,
                    false,
                    "Invalid role policy data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            if (id != rolePolicyDto.Id)
            {
                return BadRequest(new ApiResponse<RolePolicyDto>(
                    default!,
                    false,
                    "ID mismatch.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.IdMismatch
                ));
            }

            var result = await _rolePolicyRepository.UpdateAsync(id, rolePolicyDto);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/role-policies/{id}
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _rolePolicyRepository.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}