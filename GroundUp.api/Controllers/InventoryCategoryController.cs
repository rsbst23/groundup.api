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
        public async Task<ActionResult<PaginatedResponse<InventoryCategoryDto>>> Get([FromQuery] FilterParams filterParams)
        {
            var categories = await _inventoryCategoryRepository.GetAllAsync(filterParams);
            return Ok(categories);
        }

        // GET: api/inventory-categories/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryCategoryDto>> GetById(int id)
        {
            var category = await _inventoryCategoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return Ok(category);
        }

        // POST: api/inventory-categories
        [HttpPost]
        public async Task<ActionResult<InventoryCategoryDto>> Create([FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (inventoryCategoryDto == null)
            {
                return BadRequest("Invalid inventory category data.");
            }

            var createdCategory = await _inventoryCategoryRepository.AddAsync(inventoryCategoryDto);
            return CreatedAtAction(nameof(GetById), new { id = createdCategory.Id }, createdCategory);
        }

        // PUT: api/inventory-categories/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (id != inventoryCategoryDto.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var updatedCategory = await _inventoryCategoryRepository.UpdateAsync(id, inventoryCategoryDto);
            if (updatedCategory == null)
            {
                return NotFound();
            }

            return NoContent();
        }

        // DELETE: api/inventory-categories/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existingCategory = await _inventoryCategoryRepository.GetByIdAsync(id);
            if (existingCategory == null)
            {
                return NotFound();
            }

            await _inventoryCategoryRepository.DeleteAsync(id);
            return NoContent();
        }
    }
}
