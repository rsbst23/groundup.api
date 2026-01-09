using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces;

public interface IInventoryCategoryService
{
    [RequiresPermission("inventory.view")]
    Task<OperationResult<PaginatedData<InventoryCategoryDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("inventory.view")]
    Task<OperationResult<InventoryCategoryDto>> GetByIdAsync(int id);

    [RequiresPermission("inventory.create")]
    Task<OperationResult<InventoryCategoryDto>> AddAsync(InventoryCategoryDto dto);

    [RequiresPermission("inventory.update")]
    Task<OperationResult<InventoryCategoryDto>> UpdateAsync(int id, InventoryCategoryDto dto);

    [RequiresPermission("inventory.delete")]
    Task<OperationResult<bool>> DeleteAsync(int id);

    [RequiresPermission("inventory.export")]
    Task<OperationResult<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv");
}
