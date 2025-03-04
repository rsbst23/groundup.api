using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using GroundUp.core.security;
using System.Threading.Tasks;

namespace GroundUp.infrastructure.repositories
{
    public class InventoryCategoryRepository : BaseRepository<InventoryCategory, InventoryCategoryDto>, IInventoryCategoryRepository
    {
        public InventoryCategoryRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }

        [RequiresPermission("inventory.view")]
        public override Task<ApiResponse<PaginatedData<InventoryCategoryDto>>> GetAllAsync(FilterParams filterParams)
            => base.GetAllAsync(filterParams);

        [RequiresPermission("inventory.view")]
        public override Task<ApiResponse<InventoryCategoryDto>> GetByIdAsync(int id)
            => base.GetByIdAsync(id);

        [RequiresPermission("inventory.create")]
        public override Task<ApiResponse<InventoryCategoryDto>> AddAsync(InventoryCategoryDto dto)
            => base.AddAsync(dto);

        [RequiresPermission("inventory.update")]
        public override Task<ApiResponse<InventoryCategoryDto>> UpdateAsync(int id, InventoryCategoryDto dto)
            => base.UpdateAsync(id, dto);

        [RequiresPermission("inventory.delete")]
        public override Task<ApiResponse<bool>> DeleteAsync(int id)
            => base.DeleteAsync(id);
    }
}
