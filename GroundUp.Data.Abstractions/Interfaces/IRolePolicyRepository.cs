using GroundUp.core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces
{
    public interface IRolePolicyRepository
    {
        Task<ApiResponse<PaginatedData<RolePolicyDto>>> GetAllAsync(FilterParams filterParams);

        Task<ApiResponse<RolePolicyDto>> GetByIdAsync(int id);

        Task<ApiResponse<RolePolicyDto>> AddAsync(RolePolicyDto rolePolicyDto);

        Task<ApiResponse<RolePolicyDto>> UpdateAsync(int id, RolePolicyDto rolePolicyDto);

        Task<ApiResponse<bool>> DeleteAsync(int id);

        // Additional role-policy operations may be re-enabled later.
    }
}
