using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces;

/// <summary>
/// Service boundary for user-role management.
/// Controllers call services only; authorization is enforced here.
/// </summary>
public interface IUserRoleService
{
    // Internal/auth-flow helpers (no permission attributes, called from services)
    Task<ApiResponse<bool>> AssignRoleAsync(Guid userId, int tenantId, int roleId);

    Task<ApiResponse<int?>> GetRoleIdByNameAsync(int tenantId, string roleName);

    // Admin CRUD
    [RequiresPermission("userroles.view", "SYSTEMADMIN")]
    Task<ApiResponse<PaginatedData<UserRoleDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("userroles.view", "SYSTEMADMIN")]
    Task<ApiResponse<UserRoleDto>> GetByIdAsync(int id);

    [RequiresPermission("userroles.view", "SYSTEMADMIN")]
    Task<ApiResponse<UserRoleDto>> GetByNameAsync(string name);

    [RequiresPermission("userroles.create", "SYSTEMADMIN")]
    Task<ApiResponse<UserRoleDto>> AddAsync(UserRoleDto roleDto);

    [RequiresPermission("userroles.update", "SYSTEMADMIN")]
    Task<ApiResponse<UserRoleDto>> UpdateAsync(int id, UserRoleDto roleDto);

    [RequiresPermission("userroles.delete", "SYSTEMADMIN")]
    Task<ApiResponse<bool>> DeleteAsync(int id);
}
