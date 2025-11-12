using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/user-roles")]
    [ApiController]
    public class UserRoleController : ControllerBase
    {
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly ILoggingService _logger;

        public UserRoleController(
            IUserRoleRepository userRoleRepository,
            ILoggingService logger)
        {
            _userRoleRepository = userRoleRepository;
            _logger = logger;
        }

        // GET: api/user-roles
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<UserRoleDto>>>> GetAll([FromQuery] FilterParams filterParams)
        {
            var result = await _userRoleRepository.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/user-roles/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<UserRoleDto>>> GetById(int id)
        {
            var result = await _userRoleRepository.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/user-roles/by-name/{name}
        [HttpGet("by-name/{name}")]
        public async Task<ActionResult<ApiResponse<UserRoleDto>>> GetByName(string name)
        {
            var result = await _userRoleRepository.GetByNameAsync(name);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/user-roles
        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserRoleDto>>> Add([FromBody] UserRoleDto userRoleDto)
        {
            var result = await _userRoleRepository.AddAsync(userRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/user-roles/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<UserRoleDto>>> Update(int id, [FromBody] UserRoleDto userRoleDto)
        {
            var result = await _userRoleRepository.UpdateAsync(id, userRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/user-roles/{id}
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _userRoleRepository.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}