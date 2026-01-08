using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.Repositories.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class UserRoleRepository : BaseTenantRepository<UserRole, UserRoleDto>, IUserRoleRepository
    {
        public UserRoleRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
            : base(context, mapper, logger, tenantContext) { }

        public async Task<ApiResponse<UserRoleDto>> GetByNameAsync(string name)
        {
            try
            {
                var userRole = await _context.Set<UserRole>()
                    .Include(ur => ur.Role)
                    .FirstOrDefaultAsync(ur => ur.Role.Name == name);

                if (userRole == null)
                {
                    return new ApiResponse<UserRoleDto>(default!, false, $"UserRole with role name '{name}' not found.", null, 404);
                }

                var dto = _mapper.Map<UserRoleDto>(userRole);
                return new ApiResponse<UserRoleDto>(dto);
            }
            catch (Exception ex)
            {
                return new ApiResponse<UserRoleDto>(default!, false, "An error occurred while retrieving the UserRole.", new List<string> { ex.Message }, 500);
            }
        }

        public async Task<ApiResponse<bool>> AssignRoleAsync(Guid userId, int tenantId, int roleId)
        {
            try
            {
                var userRoles = _context.Set<UserRole>();

                // idempotent: do nothing if already assigned
                var exists = await userRoles.AnyAsync(ur => ur.UserId == userId && ur.TenantId == tenantId && ur.RoleId == roleId);
                if (exists)
                {
                    return new ApiResponse<bool>(true, true, "Role already assigned.");
                }

                await userRoles.AddAsync(new UserRole
                {
                    UserId = userId,
                    TenantId = tenantId,
                    RoleId = roleId
                });

                await _context.SaveChangesAsync();

                return new ApiResponse<bool>(true, true, "Role assigned successfully.");
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>(false, false, "An error occurred while assigning the role.", new List<string> { ex.Message }, 500);
            }
        }

        public async Task<ApiResponse<int?>> GetRoleIdByNameAsync(int tenantId, string roleName)
        {
            try
            {
                var roleId = await _context.Set<Role>()
                    .Where(r => r.TenantId == tenantId && r.Name == roleName)
                    .Select(r => (int?)r.Id)
                    .FirstOrDefaultAsync();

                return new ApiResponse<int?>(roleId, true, "Role lookup successful.");
            }
            catch (Exception ex)
            {
                return new ApiResponse<int?>(default, false, "An error occurred while looking up the role.", new List<string> { ex.Message }, 500);
            }
        }
    }
}
