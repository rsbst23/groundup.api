using GroundUp.core.dtos;
using GroundUp.core.entities;

namespace GroundUp.Data.Abstractions.Interfaces
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

        Task<ApiResponse<PaginatedData<UserRoleDto>>> GetAllAsync(FilterParams filterParams);

        Task<ApiResponse<UserRoleDto>> GetByIdAsync(int id);

        Task<ApiResponse<UserRoleDto>> GetByNameAsync(string name);

        Task<ApiResponse<UserRoleDto>> AddAsync(UserRoleDto roleDto);

        Task<ApiResponse<UserRoleDto>> UpdateAsync(int id, UserRoleDto roleDto);

        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}
