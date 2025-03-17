using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GroundUp.api.Controllers
{
    [EnableRateLimiting("AdminApiPolicy")]
    [Route("api/roles")]
    [ApiController]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly IRoleRepository _roleRepository;
        private readonly ILoggingService _logger;

        public RolesController(IRoleRepository roleRepository, ILoggingService logger)
        {
            _roleRepository = roleRepository;
            _logger = logger;
        }

        // GET: api/roles (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<RoleDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _roleRepository.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/roles/{name}
        [HttpGet("{name}")]
        public async Task<ActionResult<ApiResponse<RoleDto>>> GetByName(string name)
        {
            var result = await _roleRepository.GetByNameAsync(name);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/roles
        [HttpPost]
        public async Task<ActionResult<ApiResponse<RoleDto>>> Create([FromBody] CreateRoleDto roleDto)
        {
            var result = await _roleRepository.CreateAsync(roleDto);
            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/roles/{name}
        [HttpPut("{name}")]
        public async Task<ActionResult<ApiResponse<RoleDto>>> Update(string name, [FromBody] UpdateRoleDto roleDto)
        {
            var result = await _roleRepository.UpdateAsync(name, roleDto);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/roles/{name}
        [HttpDelete("{name}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(string name)
        {
            var result = await _roleRepository.DeleteAsync(name);
            return StatusCode(result.StatusCode, result);
        }
    }
}