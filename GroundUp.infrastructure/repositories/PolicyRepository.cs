using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class PolicyRepository : BaseRepository<Policy, PolicyDto>, IPolicyRepository
    {
        public PolicyRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }

        public async Task<ApiResponse<PolicyDto>> GetByNameAsync(string name)
        {
            try
            {
                var policy = await _context.Policies
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
                var permissions = await _context.PolicyPermissions
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
                var existing = await _context.PolicyPermissions
                    .Where(pp => pp.PolicyId == policyId && permissionIds.Contains(pp.PermissionId))
                    .ToListAsync();

                var toAdd = permissionIds.Except(existing.Select(pp => pp.PermissionId)).ToList();
                foreach (var permissionId in toAdd)
                {
                    _context.PolicyPermissions.Add(new PolicyPermission
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
                var policyPermission = await _context.PolicyPermissions
                    .FirstOrDefaultAsync(pp => pp.PolicyId == policyId && pp.PermissionId == permissionId);
                if (policyPermission == null)
                {
                    return new ApiResponse<bool>(false, false, "Permission not assigned to policy.", null, 404);
                }
                _context.PolicyPermissions.Remove(policyPermission);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool>(true);
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>(false, false, "An error occurred while removing permission.", new List<string> { ex.Message }, 500);
            }
        }
    }
}
