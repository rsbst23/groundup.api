using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.Api.Controllers
{
    [Route("api/system-roles")]
    [ApiController]
    [Authorize]
    public class SystemRolesController : ControllerBase
    {
        //private readonly ISystemRoleRepository _systemRoleRepository;
        //private readonly ILoggingService _logger;

        //public SystemRolesController(
        //    ISystemRoleRepository systemRoleRepository,
        //    ILoggingService logger)
        //{
        //    _systemRoleRepository = systemRoleRepository;
        //    _logger = logger;
        //}

        //// GET: api/system-roles
        //[HttpGet]
        //[RequiresPermission("system.roles.view")]
        //public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetAllRoles()
        //{
        //    var result = await _systemRoleRepository.GetAllAsync();
        //    return StatusCode(result.StatusCode, result);
        //}

        //// GET: api/system-roles/{name}
        //[HttpGet("{name}")]
        //[RequiresPermission("system.roles.view")]
        //public async Task<ActionResult<ApiResponse<RoleDto>>> GetRoleByName(string name)
        //{
        //    var result = await _systemRoleRepository.GetByNameAsync(name);
        //    return StatusCode(result.StatusCode, result);
        //}

        //// GET: api/system-roles/{name}/policies
        //[HttpGet("{name}/policies")]
        //[RequiresPermission("system.roles.view")]
        //public async Task<ActionResult<ApiResponse<List<PolicyDto>>>> GetRolePolicies(string name)
        //{
        //    var result = await _systemRoleRepository.GetRolePoliciesAsync(name);
        //    return StatusCode(result.StatusCode, result);
        //}

        //// POST: api/system-roles/{name}/policies
        //[HttpPost("{name}/policies")]
        //[RequiresPermission("system.roles.edit")]
        //public async Task<ActionResult<ApiResponse<RolePolicyDto>>> AssignPolicyToRole(
        //    string name, [FromBody] int policyId)
        //{
        //    var result = await _systemRoleRepository.AssignPolicyToRoleAsync(name, policyId);
        //    return StatusCode(result.StatusCode, result);
        //}

        //// DELETE: api/system-roles/{name}/policies/{policyId}
        //[HttpDelete("{name}/policies/{policyId}")]
        //[RequiresPermission("system.roles.edit")]
        //public async Task<ActionResult<ApiResponse<bool>>> RemovePolicyFromRole(
        //    string name, int policyId)
        //{
        //    var result = await _systemRoleRepository.RemovePolicyFromRoleAsync(name, policyId);
        //    return StatusCode(result.StatusCode, result);
        //}
    }
}
