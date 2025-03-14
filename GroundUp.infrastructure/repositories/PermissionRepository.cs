// PermissionRepository.cs
using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class PermissionRepository : BaseRepository<Permission, PermissionDto>, IPermissionRepository
    {
        public PermissionRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }

        public async Task<ApiResponse<PermissionDto>> GetByNameAsync(string name)
        {
            try
            {
                var permission = await _dbSet.FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());
                if (permission == null)
                {
                    return new ApiResponse<PermissionDto>(
                        default!,
                        false,
                        $"Permission with name '{name}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                return new ApiResponse<PermissionDto>(_mapper.Map<PermissionDto>(permission));
            }
            catch (Exception ex)
            {
                return new ApiResponse<PermissionDto>(
                    default!,
                    false,
                    "An error occurred while retrieving the permission.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<List<string>>> GetPermissionsByRoleNamesAsync(List<string> roleNames)
        {
            try
            {
                var permissions = await _context.RolePermissions
                    .Where(rp => roleNames.Contains(rp.RoleName))
                    .Include(rp => rp.Permission)
                    .Select(rp => rp.Permission.Name)
                    .Distinct()
                    .ToListAsync();

                return new ApiResponse<List<string>>(permissions);
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<string>>(
                    new List<string>(),
                    false,
                    "An error occurred while retrieving permissions by role names.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }
    }

    // RolePermissionRepository.cs
    public class RolePermissionRepository : BaseRepository<RolePermission, RolePermissionDto>, IRolePermissionRepository
    {
        public RolePermissionRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }

        public async Task<ApiResponse<List<RolePermissionDto>>> GetByRoleNameAsync(string roleName)
        {
            try
            {
                var rolePermissions = await _context.RolePermissions
                    .Where(rp => rp.RoleName.ToLower() == roleName.ToLower())
                    .Include(rp => rp.Permission)
                    .ToListAsync();

                var result = _mapper.Map<List<RolePermissionDto>>(rolePermissions);
                return new ApiResponse<List<RolePermissionDto>>(result);
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<RolePermissionDto>>(
                    new List<RolePermissionDto>(),
                    false,
                    "An error occurred while retrieving role permissions.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(string roleName, int permissionId)
        {
            try
            {
                var rolePermission = await _context.RolePermissions
                    .FirstOrDefaultAsync(rp => rp.RoleName.ToLower() == roleName.ToLower() && rp.PermissionId == permissionId);

                if (rolePermission == null)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Role permission not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                _context.RolePermissions.Remove(rolePermission);
                await _context.SaveChangesAsync();

                return new ApiResponse<bool>(true, true, "Role permission deleted successfully");
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>(
                    false,
                    false,
                    "An error occurred while deleting the role permission.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> DeleteByRoleNameAsync(string roleName)
        {
            try
            {
                var rolePermissions = await _context.RolePermissions
                    .Where(rp => rp.RoleName.ToLower() == roleName.ToLower())
                    .ToListAsync();

                if (!rolePermissions.Any())
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"No permissions found for role '{roleName}'",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                _context.RolePermissions.RemoveRange(rolePermissions);
                await _context.SaveChangesAsync();

                return new ApiResponse<bool>(true, true, $"All permissions for role '{roleName}' deleted successfully");
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>(
                    false,
                    false,
                    "An error occurred while deleting role permissions.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }
    }
}