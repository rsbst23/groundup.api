using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.security;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GroundUp.core.interfaces
{
    public interface IUserTenantRepository
    {
        // Read operations
        Task<List<UserTenantDto>> GetTenantsForUserAsync(Guid userId);
        Task<UserTenantDto?> GetUserTenantAsync(Guid userId, int tenantId);
        
        // Write operations
        [RequiresPermission("users.tenants.assign")]
        Task<UserTenantDto> AssignUserToTenantAsync(Guid userId, int tenantId, bool isAdmin = false);
        
        [RequiresPermission("users.tenants.remove")]
        Task<bool> RemoveUserFromTenantAsync(Guid userId, int tenantId);
    }
}
