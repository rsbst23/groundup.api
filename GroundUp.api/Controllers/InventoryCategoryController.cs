using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/inventory-categories")]
    [ApiController]
    public class InventoryCategoryController : ControllerBase
    {
        private readonly IInventoryCategoryRepository _inventoryCategoryRepository;

        public InventoryCategoryController(IInventoryCategoryRepository inventoryCategoryRepository)
        {
            _inventoryCategoryRepository = inventoryCategoryRepository;
        }

        // GET: api/inventory-categories (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<InventoryCategoryDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _inventoryCategoryRepository.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/inventory-categories/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<InventoryCategoryDto>>> GetById(int id)
        {
            var result = await _inventoryCategoryRepository.GetByIdAsync(id);

            return StatusCode(result.StatusCode, result);
        }

        // POST: api/inventory-categories
        [HttpPost]
        public async Task<ActionResult<ApiResponse<InventoryCategoryDto>>> Create([FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (inventoryCategoryDto == null)
            {
                return BadRequest(new ApiResponse<InventoryCategoryDto>(
                    default!,
                    false,
                    "Invalid inventory category data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _inventoryCategoryRepository.AddAsync(inventoryCategoryDto);

            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/inventory-categories/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<InventoryCategoryDto>>> Update(int id, [FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (inventoryCategoryDto == null)
            {
                return BadRequest(new ApiResponse<InventoryCategoryDto>(
                    default!,
                    false,
                    "Invalid inventory category data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            if (id != inventoryCategoryDto.Id)
            {
                return BadRequest(new ApiResponse<InventoryCategoryDto>(
                    default!,
                    false,
                    "ID mismatch.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.IdMismatch
                ));
            }

            var result = await _inventoryCategoryRepository.UpdateAsync(id, inventoryCategoryDto);

            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/inventory-categories/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _inventoryCategoryRepository.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
