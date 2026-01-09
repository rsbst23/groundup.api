using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IInventoryCategoryRepository
    {
        Task<OperationResult<PaginatedData<InventoryCategoryDto>>> GetAllAsync(FilterParams filterParams);
        Task<OperationResult<InventoryCategoryDto>> GetByIdAsync(int id);
        Task<OperationResult<InventoryCategoryDto>> AddAsync(InventoryCategoryDto dto);
        Task<OperationResult<InventoryCategoryDto>> UpdateAsync(int id, InventoryCategoryDto dto);
        Task<OperationResult<bool>> DeleteAsync(int id);
        Task<OperationResult<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv");
    }
}
