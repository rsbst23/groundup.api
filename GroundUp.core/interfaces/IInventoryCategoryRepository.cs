using GroundUp.core.dtos;
using System.Threading.Tasks;

namespace GroundUp.core.interfaces
{
    public interface IInventoryCategoryRepository
    {
        Task<ApiResponse<PaginatedData<InventoryCategoryDto>>> GetAllAsync(FilterParams filterParams);
        Task<ApiResponse<InventoryCategoryDto>> GetByIdAsync(int id);
        Task<ApiResponse<InventoryCategoryDto>> AddAsync(InventoryCategoryDto dto);
        Task<ApiResponse<InventoryCategoryDto>> UpdateAsync(int id, InventoryCategoryDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}
