using GroundUp.core.dtos;
using GroundUp.core.security;
using System.Threading.Tasks;

namespace GroundUp.core.interfaces
{
    public interface IInventoryCategoryRepository
    {
        [RequiresPermission("inventory.view")]
        Task<ApiResponse<PaginatedData<InventoryCategoryDto>>> GetAllAsync(FilterParams filterParams);

        [RequiresPermission("inventory.view")]
        Task<ApiResponse<InventoryCategoryDto>> GetByIdAsync(int id);

        [RequiresPermission("inventory.create")]
        Task<ApiResponse<InventoryCategoryDto>> AddAsync(InventoryCategoryDto dto);

        [RequiresPermission("inventory.update")]
        Task<ApiResponse<InventoryCategoryDto>> UpdateAsync(int id, InventoryCategoryDto dto);

        [RequiresPermission("inventory.delete")]
        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}
