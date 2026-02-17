using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;

namespace GroundUp.Services.Core.Services;

public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;

    public RoleService(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public Task<ApiResponse<PaginatedData<RoleDto>>> GetAllAsync(FilterParams filterParams) =>
        _roleRepository.GetAllAsync(filterParams);

    public Task<ApiResponse<RoleDto>> GetByIdAsync(int id) =>
        _roleRepository.GetByIdAsync(id);

    public Task<ApiResponse<RoleDto>> AddAsync(RoleDto roleDto) =>
        _roleRepository.AddAsync(roleDto);

    public Task<ApiResponse<RoleDto>> UpdateAsync(int id, RoleDto roleDto) =>
        _roleRepository.UpdateAsync(id, roleDto);

    public Task<ApiResponse<bool>> DeleteAsync(int id) =>
        _roleRepository.DeleteAsync(id);
}
