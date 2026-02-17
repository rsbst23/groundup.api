using AutoMapper;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.Data.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.Data.Core.Repositories;

public class PolicyRepository : BaseTenantRepository<Policy, PolicyDto>, IPolicyRepository
{
    public PolicyRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
        : base(context, mapper, logger, tenantContext) { }

    public async Task<ApiResponse<PolicyDto>> GetByNameAsync(string name)
    {
        try
        {
            var policy = await _context.Set<Policy>()
                .Include(p => p.PolicyPermissions)
                .ThenInclude(pp => pp.Permission)
                .FirstOrDefaultAsync(p => p.Name == name);

            if (policy == null)
            {
                return new ApiResponse<PolicyDto>(default!, false, $"Policy '{name}' not found.", null, 404);
            }

            var dto = _mapper.Map<PolicyDto>(policy);
            dto.Permissions = policy.PolicyPermissions.Select(pp => _mapper.Map<PermissionDto>(pp.Permission)).ToList();
            return new ApiResponse<PolicyDto>(dto);
        }
        catch (Exception ex)
        {
            return new ApiResponse<PolicyDto>(default!, false, "An error occurred while retrieving the policy.", new List<string> { ex.Message }, 500);
        }
    }

    public async Task<ApiResponse<List<PermissionDto>>> GetPolicyPermissionsAsync(int policyId)
    {
        try
        {
            var permissions = await _context.Set<PolicyPermission>()
                .Where(pp => pp.PolicyId == policyId)
                .Include(pp => pp.Permission)
                .Select(pp => _mapper.Map<PermissionDto>(pp.Permission))
                .ToListAsync();

            return new ApiResponse<List<PermissionDto>>(permissions);
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<PermissionDto>>(new List<PermissionDto>(), false, "An error occurred while retrieving policy permissions.", new List<string> { ex.Message }, 500);
        }
    }

    public async Task<ApiResponse<bool>> AssignPermissionsToPolicyAsync(int policyId, List<int> permissionIds)
    {
        try
        {
            var policyPermissions = _context.Set<PolicyPermission>();

            var existing = await policyPermissions
                .Where(pp => pp.PolicyId == policyId && permissionIds.Contains(pp.PermissionId))
                .ToListAsync();

            var toAdd = permissionIds.Except(existing.Select(pp => pp.PermissionId)).ToList();
            foreach (var permissionId in toAdd)
            {
                policyPermissions.Add(new PolicyPermission
                {
                    PolicyId = policyId,
                    PermissionId = permissionId
                });
            }
            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(true);
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>(false, false, "An error occurred while assigning permissions.", new List<string> { ex.Message }, 500);
        }
    }

    public async Task<ApiResponse<bool>> RemovePermissionFromPolicyAsync(int policyId, int permissionId)
    {
        try
        {
            var policyPermissions = _context.Set<PolicyPermission>();

            var policyPermission = await policyPermissions
                .FirstOrDefaultAsync(pp => pp.PolicyId == policyId && pp.PermissionId == permissionId);
            if (policyPermission == null)
            {
                return new ApiResponse<bool>(false, false, "Permission not assigned to policy.", null, 404);
            }
            policyPermissions.Remove(policyPermission);
            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(true);
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>(false, false, "An error occurred while removing permission.", new List<string> { ex.Message }, 500);
        }
    }
}
