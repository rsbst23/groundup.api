using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.security;

namespace GroundUp.core.interfaces
{
    public interface IUserRoleRepository
    {
        /// <summary>
        /// Assigns a role to a user within a tenant (idempotent).
        /// Used by auth flows (e.g., enterprise SSO auto-join) to attach a default role.
        /// </summary>
        Task<ApiResponse<bool>> AssignRoleAsync(Guid userId, int tenantId, int roleId);

        /// <summary>
        /// Gets the role id for a tenant-scoped role by name.
        /// Convenience for flows that need to fall back to a default role (e.g., "Member").
        /// </summary>
        Task<ApiResponse<int?>> GetRoleIdByNameAsync(int tenantId, string roleName);

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
}
