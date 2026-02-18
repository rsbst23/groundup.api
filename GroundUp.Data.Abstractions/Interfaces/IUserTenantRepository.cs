using GroundUp.Core.dtos;
using GroundUp.Core.entities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GroundUp.Data.Abstractions.Interfaces
{
    public interface IUserTenantRepository
    {
        // Read operations
        Task<List<UserTenantDto>> GetTenantsForUserAsync(Guid userId);
        Task<UserTenantDto?> GetUserTenantAsync(Guid userId, int tenantId);

        /// <summary>
        /// Returns true if the tenant has at least one member.
        /// Intended for auth flows that need a lightweight check.
        /// </summary>
        Task<bool> TenantHasAnyMembersAsync(int tenantId);

        /// <summary>
        /// Resolve UserTenant by realm name and external user ID (Keycloak sub).
        /// Used during auth callback to determine tenant membership.
        /// </summary>
        Task<UserTenantDto?> GetByRealmAndExternalUserIdAsync(string realmName, string externalUserId);

        // Write operations
        Task<UserTenantDto> AssignUserToTenantAsync(Guid userId, int tenantId, bool isAdmin = false, string? externalUserId = null);

        Task<bool> RemoveUserFromTenantAsync(Guid userId, int tenantId);
    }
}
