using System.Threading.Tasks;

namespace GroundUp.core.interfaces
{
    public interface IPermissionService
    {
        Task<bool> HasPermission(string userId, string permission);
        Task<bool> HasAnyPermission(string userId, string[] permissions);
        Task<IEnumerable<string>> GetUserPermissions(string userId);

        // Additional methods for role-based permission management
        Task AssignPermissionToRole(string roleName, string permission);
        Task RemovePermissionFromRole(string roleName, string permission);
        Task<IEnumerable<string>> GetRolePermissions(string roleName);
    }
}
