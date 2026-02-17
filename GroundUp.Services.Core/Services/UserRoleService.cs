using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;

namespace GroundUp.Services.Core.Services;

public sealed class UserRoleService : IUserRoleService
{
    private readonly IUserRoleRepository _repo;

    public UserRoleService(IUserRoleRepository repo)
    {
        _repo = repo;
    }

    public Task<ApiResponse<bool>> AssignRoleAsync(Guid userId, int tenantId, int roleId) =>
        _repo.AssignRoleAsync(userId, tenantId, roleId);

    public Task<ApiResponse<int?>> GetRoleIdByNameAsync(int tenantId, string roleName) =>
        _repo.GetRoleIdByNameAsync(tenantId, roleName);

    public Task<ApiResponse<PaginatedData<UserRoleDto>>> GetAllAsync(FilterParams filterParams) =>
        _repo.GetAllAsync(filterParams);

    public Task<ApiResponse<UserRoleDto>> GetByIdAsync(int id) =>
        _repo.GetByIdAsync(id);

    public Task<ApiResponse<UserRoleDto>> GetByNameAsync(string name) =>
        _repo.GetByNameAsync(name);

    public Task<ApiResponse<UserRoleDto>> AddAsync(UserRoleDto roleDto) =>
        _repo.AddAsync(roleDto);

    public Task<ApiResponse<UserRoleDto>> UpdateAsync(int id, UserRoleDto roleDto) =>
        _repo.UpdateAsync(id, roleDto);

    public Task<ApiResponse<bool>> DeleteAsync(int id) =>
        _repo.DeleteAsync(id);
}
