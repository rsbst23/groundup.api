using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class UserRoleRepository : BaseRepository<UserRole, UserRoleDto>, IUserRoleRepository
    {
        public UserRoleRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }

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
    }
}
