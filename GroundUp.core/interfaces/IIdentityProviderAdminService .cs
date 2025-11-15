using GroundUp.core.dtos;
using GroundUp.core.entities;

namespace GroundUp.core.interfaces
{
    public interface IIdentityProviderAdminService
    {
        // Role Management
        Task<List<SystemRoleDto>> GetAllRolesAsync();
        Task<SystemRoleDto?> GetRoleByNameAsync(string name);
        Task<SystemRoleDto> CreateRoleAsync(CreateSystemRoleDto roleDto);
        Task<SystemRoleDto?> UpdateRoleAsync(string name, UpdateRoleDto roleDto);
        Task<bool> DeleteRoleAsync(string name);

        // User-Role Management
        Task<List<SystemRoleDto>> GetUserRolesAsync(string userId);
        Task<bool> AssignRoleToUserAsync(string userId, string roleName);
        Task<bool> AssignRolesToUserAsync(string userId, List<string> roleNames);
        Task<bool> RemoveRoleFromUserAsync(string userId, string roleName);

        // User Management
        Task<List<UserSummaryDto>> GetAllUsersAsync();
        Task<UserDetailsDto?> GetUserByIdAsync(string userId);
        Task<UserDetailsDto?> GetUserByUsernameAsync(string username);
        Task<UserDetailsDto> CreateUserAsync(CreateUserDto userDto);
        Task<UserDetailsDto?> UpdateUserAsync(string userId, UpdateUserDto userDto);
        Task<bool> DeleteUserAsync(string userId);
        Task<bool> SetUserEnabledAsync(string userId, bool enabled);
        Task<bool> SendPasswordResetEmailAsync(string userId);
    }
}