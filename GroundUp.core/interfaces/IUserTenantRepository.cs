using GroundUp.core.dtos;
using GroundUp.core.entities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GroundUp.core.interfaces
{
    public interface IUserTenantRepository
    {
        Task<List<UserTenantDto>> GetTenantsForUserAsync(Guid userId);
        Task<UserTenantDto?> GetUserTenantAsync(Guid userId, int tenantId);
    }
}
