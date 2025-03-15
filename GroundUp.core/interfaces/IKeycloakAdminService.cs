using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces
{
    public interface IKeycloakAdminService
    {
        // Role Management
        [RequiresPermission("roles.view", "ADMIN")]
        Task<List<RoleDto>> GetAllRolesAsync();

        [RequiresPermission("roles.view", "ADMIN")]
        Task<RoleDto?> GetRoleByNameAsync(string name);

        [RequiresPermission("roles.create", "ADMIN")]
        Task<RoleDto> CreateRoleAsync(CreateRoleDto roleDto);

        [RequiresPermission("roles.update", "ADMIN")]
        Task<RoleDto?> UpdateRoleAsync(string name, UpdateRoleDto roleDto);

        [RequiresPermission("roles.delete", "ADMIN")]
        Task<bool> DeleteRoleAsync(string name);

        // User-Role Management
        [RequiresPermission("users.roles.view", "ADMIN")]
        Task<List<RoleDto>> GetUserRolesAsync(string userId);

        [RequiresPermission("users.roles.assign", "ADMIN")]
        Task<bool> AssignRoleToUserAsync(string userId, string roleName);

        [RequiresPermission("users.roles.assign", "ADMIN")]
        Task<bool> AssignRolesToUserAsync(string userId, List<string> roleNames);

        [RequiresPermission("users.roles.remove", "ADMIN")]
        Task<bool> RemoveRoleFromUserAsync(string userId, string roleName);

        // User Management
        [RequiresPermission("users.view", "ADMIN")]
        Task<List<UserSummaryDto>> GetAllUsersAsync();

        [RequiresPermission("users.view", "ADMIN")]
        Task<UserDetailsDto?> GetUserByIdAsync(string userId);

        [RequiresPermission("users.view", "ADMIN")]
        Task<UserDetailsDto?> GetUserByUsernameAsync(string username);
    }
}