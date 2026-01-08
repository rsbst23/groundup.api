using GroundUp.core.dtos;
using GroundUp.core.interfaces;

namespace GroundUp.Services.Inventory;

public sealed class InventoryCategoryService : IInventoryCategoryService
{
    private readonly IInventoryCategoryRepository _repo;

    public InventoryCategoryService(IInventoryCategoryRepository repo)
    {
        _repo = repo;
    }

    public Task<OperationResult<PaginatedData<InventoryCategoryDto>>> GetAllAsync(FilterParams filterParams)
        => _repo.GetAllAsync(filterParams);

    public Task<OperationResult<InventoryCategoryDto>> GetByIdAsync(int id)
        => _repo.GetByIdAsync(id);

    public Task<OperationResult<InventoryCategoryDto>> AddAsync(InventoryCategoryDto dto)
        => _repo.AddAsync(dto);

    public Task<OperationResult<InventoryCategoryDto>> UpdateAsync(int id, InventoryCategoryDto dto)
        => _repo.UpdateAsync(id, dto);

    public Task<OperationResult<bool>> DeleteAsync(int id)
        => _repo.DeleteAsync(id);

    public Task<OperationResult<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv")
        => _repo.ExportAsync(filterParams, format);
}
