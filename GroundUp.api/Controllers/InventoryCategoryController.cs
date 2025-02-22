using GroundUp.core.interfaces;
using GroundUp.core.dtos;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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
            return Ok(result);
        }

        // GET: api/inventory-categories/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<InventoryCategoryDto>>> GetById(int id)
        {
            var result = await _inventoryCategoryRepository.GetByIdAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        // POST: api/inventory-categories
        [HttpPost]
        public async Task<ActionResult<ApiResponse<InventoryCategoryDto>>> Create([FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (inventoryCategoryDto == null)
            {
                return BadRequest(new ApiResponse<InventoryCategoryDto>(default, false, "Invalid inventory category data."));
            }

            var result = await _inventoryCategoryRepository.AddAsync(inventoryCategoryDto);

            if (!result.Success)
            {
                return BadRequest(result); // Return error details if creation fails
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        // PUT: api/inventory-categories/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<InventoryCategoryDto>>> Update(int id, [FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (id != inventoryCategoryDto.Id)
            {
                return BadRequest(new ApiResponse<InventoryCategoryDto>(default, false, "ID mismatch."));
            }

            var result = await _inventoryCategoryRepository.UpdateAsync(id, inventoryCategoryDto);

            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result); // Ensure response always contains ApiResponse<T>
        }

        // DELETE: api/inventory-categories/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _inventoryCategoryRepository.DeleteAsync(id);

            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result); // Return ApiResponse<bool> instead of empty response
        }
    }
}
