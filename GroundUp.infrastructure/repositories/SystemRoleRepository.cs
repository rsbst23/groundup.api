using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.core;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class SystemRoleRepository : ISystemRoleRepository
    {
        private readonly IIdentityProviderAdminService _identityProviderAdminService;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILoggingService _logger;

        public SystemRoleRepository(
            IIdentityProviderAdminService identityProviderAdminService,
            ApplicationDbContext context,
            IMapper mapper,
            ILoggingService logger)
        {
            _identityProviderAdminService = identityProviderAdminService;
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<List<RoleDto>>> GetAllAsync()
        {
            try
            {
                // Get roles from Keycloak
                var keycloakRoles = await _identityProviderAdminService.GetAllRolesAsync();

                // Map to our RoleDto with System as RoleType
                var roleDtos = keycloakRoles.Select(kr => new RoleDto
                {
                    Name = kr.Name,
                    Description = kr.Description,
                    RoleType = RoleType.System
                }).ToList();

                return new ApiResponse<List<RoleDto>>(roleDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving system roles: {ex.Message}", ex);
                return new ApiResponse<List<RoleDto>>(
                    new List<RoleDto>(),
                    false,
                    "Failed to retrieve system roles",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<RoleDto>> GetByNameAsync(string name)
        {
            try
            {
                var role = await _identityProviderAdminService.GetRoleByNameAsync(name);

                if (role == null)
                {
                    return new ApiResponse<RoleDto>(
                        default!,
                        false,
                        $"System role '{name}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                var roleDto = new RoleDto
                {
                    Name = role.Name,
                    Description = role.Description,
                    RoleType = RoleType.System
                };

                return new ApiResponse<RoleDto>(roleDto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving system role '{name}': {ex.Message}", ex);
                return new ApiResponse<RoleDto>(
                    default!,
                    false,
                    $"Failed to retrieve system role '{name}'",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<List<PolicyDto>>> GetRolePoliciesAsync(string roleName)
        {
            try
            {
                // Verify role exists in Keycloak
                var role = await _identityProviderAdminService.GetRoleByNameAsync(roleName);
                if (role == null)
                {
                    return new ApiResponse<List<PolicyDto>>(
                        new List<PolicyDto>(),
                        false,
                        $"System role '{roleName}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                // Get policies assigned to the role from our database
                var policies = await _context.RolePolicies
                    .Where(rp => rp.RoleName == roleName && rp.RoleType == RoleType.System)
                    .Join(_context.Policies,
                          rp => rp.PolicyId,
                          p => p.Id,
                          (rp, p) => p)
                    .ToListAsync();

                var policyDtos = _mapper.Map<List<PolicyDto>>(policies);

                // Load permissions for each policy
                foreach (var policyDto in policyDtos)
                {
                    var permissions = await _context.PolicyPermissions
                        .Where(pp => pp.PolicyId == policyDto.Id)
                        .Join(_context.Permissions,
                              pp => pp.PermissionId,
                              p => p.Id,
                              (pp, p) => p)
                        .ToListAsync();

                    policyDto.Permissions = _mapper.Map<List<PermissionDto>>(permissions);
                }

                return new ApiResponse<List<PolicyDto>>(policyDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving policies for system role '{roleName}': {ex.Message}", ex);
                return new ApiResponse<List<PolicyDto>>(
                    new List<PolicyDto>(),
                    false,
                    $"Failed to retrieve policies for system role '{roleName}'",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<RolePolicyDto>> AssignPolicyToRoleAsync(string roleName, int policyId)
        {
            try
            {
                // Verify role exists in Keycloak
                var role = await _identityProviderAdminService.GetRoleByNameAsync(roleName);
                if (role == null)
                {
                    return new ApiResponse<RolePolicyDto>(
                        default!,
                        false,
                        $"System role '{roleName}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                // Verify policy exists
                var policy = await _context.Policies.FindAsync(policyId);
                if (policy == null)
                {
                    return new ApiResponse<RolePolicyDto>(
                        default!,
                        false,
                        $"Policy with ID {policyId} not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                // Check if mapping already exists
                var existingMapping = await _context.RolePolicies
                    .FirstOrDefaultAsync(rp => rp.RoleName == roleName &&
                                             rp.RoleType == RoleType.System &&
                                             rp.PolicyId == policyId);

                if (existingMapping != null)
                {
                    return new ApiResponse<RolePolicyDto>(
                        _mapper.Map<RolePolicyDto>(existingMapping),
                        true,
                        $"Policy already assigned to system role '{roleName}'"
                    );
                }

                // Create new mapping
                var rolePolicy = new RolePolicy
                {
                    RoleName = roleName,
                    RoleType = RoleType.System,
                    PolicyId = policyId,
                    CreatedDate = DateTime.UtcNow
                };

                _context.RolePolicies.Add(rolePolicy);
                await _context.SaveChangesAsync();

                var rolePolicyDto = _mapper.Map<RolePolicyDto>(rolePolicy);
                rolePolicyDto.PolicyName = policy.Name;

                return new ApiResponse<RolePolicyDto>(
                    rolePolicyDto,
                    true,
                    $"Policy successfully assigned to system role '{roleName}'"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error assigning policy to system role '{roleName}': {ex.Message}", ex);
                return new ApiResponse<RolePolicyDto>(
                    default!,
                    false,
                    $"Failed to assign policy to system role '{roleName}'",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> RemovePolicyFromRoleAsync(string roleName, int policyId)
        {
            try
            {
                // Find the role-policy mapping
                var rolePolicy = await _context.RolePolicies
                    .FirstOrDefaultAsync(rp => rp.RoleName == roleName &&
                                             rp.RoleType == RoleType.System &&
                                             rp.PolicyId == policyId);

                if (rolePolicy == null)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"Policy with ID {policyId} is not assigned to system role '{roleName}'",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                _context.RolePolicies.Remove(rolePolicy);
                await _context.SaveChangesAsync();

                return new ApiResponse<bool>(
                    true,
                    true,
                    $"Policy successfully removed from system role '{roleName}'"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error removing policy from system role '{roleName}': {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    $"Failed to remove policy from system role '{roleName}'",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }
    }
}
