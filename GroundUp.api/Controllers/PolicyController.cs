using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/policies")]
    [ApiController]
    public class PolicyController : ControllerBase
    {
        private readonly IPolicyRepository _policyRepository;

        public PolicyController(IPolicyRepository policyRepository)
        {
            _policyRepository = policyRepository;
        }

        // GET: api/policies (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<PolicyDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _policyRepository.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/policies/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<PolicyDto>>> GetById(int id)
        {
            var result = await _policyRepository.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/policies
        [HttpPost]
        public async Task<ActionResult<ApiResponse<PolicyDto>>> Create([FromBody] PolicyDto policyDto)
        {
            if (policyDto == null)
            {
                return BadRequest(new ApiResponse<PolicyDto>(
                    default!,
                    false,
                    "Invalid policy data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _policyRepository.AddAsync(policyDto);
            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/policies/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<PolicyDto>>> Update(int id, [FromBody] PolicyDto policyDto)
        {
            if (policyDto == null)
            {
                return BadRequest(new ApiResponse<PolicyDto>(
                    default!,
                    false,
                    "Invalid policy data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            if (id != policyDto.Id)
            {
                return BadRequest(new ApiResponse<PolicyDto>(
                    default!,
                    false,
                    "ID mismatch.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.IdMismatch
                ));
            }

            var result = await _policyRepository.UpdateAsync(id, policyDto);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/policies/{id}
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _policyRepository.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/policies/{id}/permissions
        [HttpGet("{id:int}/permissions")]
        public async Task<ActionResult<ApiResponse<List<PermissionDto>>>> GetPolicyPermissions(int id)
        {
            var result = await _policyRepository.GetPolicyPermissionsAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/policies/{id}/permissions
        [HttpPost("{id:int}/permissions")]
        public async Task<ActionResult<ApiResponse<bool>>> AssignPermissionsToPolicy(int id, [FromBody] List<int> permissionIds)
        {
            var result = await _policyRepository.AssignPermissionsToPolicyAsync(id, permissionIds);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/policies/{id}/permissions/{permissionId}
        [HttpDelete("{id:int}/permissions/{permissionId:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> RemovePermissionFromPolicy(int id, int permissionId)
        {
            var result = await _policyRepository.RemovePermissionFromPolicyAsync(id, permissionId);
            return StatusCode(result.StatusCode, result);
        }
    }
}