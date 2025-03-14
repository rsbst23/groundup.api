using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using GroundUp.infrastructure.data;

namespace GroundUp.infrastructure.repositories
{
    public class InventoryCategoryRepository : BaseRepository<InventoryCategory, InventoryCategoryDto>, IInventoryCategoryRepository
    {
        private readonly ILoggingService _logger;

        public InventoryCategoryRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger) : base(context, mapper, logger)
        {
            _logger = logger;
        }

        [RequiresPermission("inventory.view")]
        public override async Task<ApiResponse<PaginatedData<InventoryCategoryDto>>> GetAllAsync(FilterParams filterParams)
            => await base.GetAllAsync(filterParams);

        [RequiresPermission("inventory.view")]
        public override async Task<ApiResponse<InventoryCategoryDto>> GetByIdAsync(int id)
            => await base.GetByIdAsync(id);

        [RequiresPermission("inventory.create")]
        public override async Task<ApiResponse<InventoryCategoryDto>> AddAsync(InventoryCategoryDto dto)
            => await base.AddAsync(dto);

        [RequiresPermission("inventory.update")]
        public override async Task<ApiResponse<InventoryCategoryDto>> UpdateAsync(int id, InventoryCategoryDto dto)
            => await base.UpdateAsync(id, dto);

        [RequiresPermission("inventory.delete")]
        public override async Task<ApiResponse<bool>> DeleteAsync(int id)
            => await base.DeleteAsync(id);

        [RequiresPermission("inventory.export")]
        public override async Task<ApiResponse<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv")
            => await base.ExportAsync(filterParams, format);
    }
}
