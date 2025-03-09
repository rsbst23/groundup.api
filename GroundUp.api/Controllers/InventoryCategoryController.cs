using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace GroundUp.api.Controllers
{
    [Route("api/inventory-categories")]
    [ApiController]
    //[Authorize]
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

        // GET: api/inventory-categories/export
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] string format = "csv", [FromQuery] string? sortBy = null, [FromQuery] bool exportAll = true, [FromQuery] FilterParams filterParams = null)
        {
            // Use null-coalescing for filterParams
            filterParams ??= new FilterParams();

            // If exportAll is true, set a large page size to get all records
            if (exportAll)
            {
                filterParams.PageSize = 10000;  // Set to a high number
                filterParams.PageNumber = 1;
            }

            // Set sorting parameter - respects your existing sort pattern with hyphen prefix
            if (!string.IsNullOrEmpty(sortBy))
            {
                filterParams.SortBy = sortBy;
            }

            // Get export data
            var result = await _inventoryCategoryRepository.ExportAsync(filterParams, format);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            // Set the content type and filename based on the requested format
            string contentType;
            string filename;

            switch (format.ToLower())
            {
                case "xlsx":
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    filename = $"inventory-categories-{DateTime.Now:yyyy-MM-dd}.xlsx";
                    break;
                case "json":
                    contentType = "application/json";
                    filename = $"inventory-categories-{DateTime.Now:yyyy-MM-dd}.json";
                    break;
                case "csv":
                default:
                    contentType = "text/csv";
                    filename = $"inventory-categories-{DateTime.Now:yyyy-MM-dd}.csv";
                    break;
            }

            // Set response headers
            Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{filename}\"");

            // Return the file
            return File(result.Data, contentType, filename);
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