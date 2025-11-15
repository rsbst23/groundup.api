using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces
{
    public interface IUserRepository
    {
        // Basic CRUD Operations
        [RequiresPermission("users.view")]
        Task<ApiResponse<PaginatedData<UserSummaryDto>>> GetAllAsync(FilterParams filterParams);

        [RequiresPermission("users.view")]
        Task<ApiResponse<UserDetailsDto>> GetByIdAsync(string userId);

        [RequiresPermission("users.create")]
        Task<ApiResponse<UserDetailsDto>> AddAsync(CreateUserDto userDto);

        [RequiresPermission("users.update")]
        Task<ApiResponse<UserDetailsDto>> UpdateAsync(string userId, UpdateUserDto userDto);

        [RequiresPermission("users.delete")]
        Task<ApiResponse<bool>> DeleteAsync(string userId);

        // System Role Management (Keycloak roles)
        [RequiresPermission("users.roles.view")]
        Task<ApiResponse<List<SystemRoleDto>>> GetUserSystemRolesAsync(string userId);

        [RequiresPermission("users.roles.assign")]
        Task<ApiResponse<bool>> AssignSystemRoleToUserAsync(string userId, string roleName);

        [RequiresPermission("users.roles.remove")]
        Task<ApiResponse<bool>> RemoveSystemRoleFromUserAsync(string userId, string roleName);

        // Enable/Disable User
        [RequiresPermission("users.update")]
        Task<ApiResponse<bool>> SetUserEnabledAsync(string userId, bool enabled);

        // Password Reset
        [RequiresPermission("users.update")]
        Task<ApiResponse<bool>> SendPasswordResetEmailAsync(string userId);
    }
}
