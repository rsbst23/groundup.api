using GroundUp.Core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces;

public interface IRoleRepository
{
    Task<ApiResponse<PaginatedData<RoleDto>>> GetAllAsync(FilterParams filterParams);

    Task<ApiResponse<RoleDto>> GetByIdAsync(int id);

    Task<ApiResponse<RoleDto>> AddAsync(RoleDto dto);

    Task<ApiResponse<RoleDto>> UpdateAsync(int id, RoleDto dto);

    Task<ApiResponse<bool>> DeleteAsync(int id);
}
