using FluentAssertions;
using GroundUp.core.dtos;
using Newtonsoft.Json;
using System.Text;

namespace GroundUp.Tests.Integration
{
    public class InventoryCategoryIntegrationTests : BaseIntegrationTest
    {
        public InventoryCategoryIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

        // Get all categories (Paginated)
        [Fact]
        public async Task Get_AllInventoryCategories_ReturnsSuccessAndCorrectData()
        {
            var response = await _client.GetAsync("/api/inventory-categories");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryCategoryDto>>(content);

            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(2);

            var categories = result.Data.ToList();
            categories[0].Name.Should().Be("Electronics");
            categories[1].Name.Should().Be("Books");
        }

        // Get category by ID
        [Fact]
        public async Task Get_InventoryCategoryById_ReturnsCorrectCategory()
        {
            var response = await _client.GetAsync("/api/inventory-categories/1");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<InventoryCategoryDto>(content);

            result.Should().NotBeNull();
            result.Name.Should().Be("Electronics");
        }

        // Create new category
        [Fact]
        public async Task Post_CreateInventoryCategory_ReturnsCreatedCategory()
        {
            var newCategory = new
            {
                Name = "Clothing"
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(newCategory), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("/api/inventory-categories", jsonContent);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var createdCategory = JsonConvert.DeserializeObject<InventoryCategoryDto>(content);

            createdCategory.Should().NotBeNull();
            createdCategory.Name.Should().Be("Clothing");
        }

        // Update existing category
        [Fact]
        public async Task Put_UpdateInventoryCategory_ReturnsNoContent()
        {
            var updatedCategory = new
            {
                Id = 1,
                Name = "Updated Electronics"
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(updatedCategory), Encoding.UTF8, "application/json");

            var response = await _client.PutAsync("/api/inventory-categories/1", jsonContent);
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

            // Verify the update
            var getResponse = await _client.GetAsync("/api/inventory-categories/1");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<InventoryCategoryDto>(getContent);

            result.Name.Should().Be("Updated Electronics");
        }

        // Delete category
        [Fact]
        public async Task Delete_InventoryCategory_ReturnsNoContentAndRemovesCategory()
        {
            var response = await _client.DeleteAsync("/api/inventory-categories/1");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

            var getResponse = await _client.GetAsync("/api/inventory-categories/1");
            getResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }

        // Sorting Tests
        [Theory]
        [InlineData("Name", "Books", "Electronics")]  // Ascending
        [InlineData("-Name", "Electronics", "Books")] // Descending
        public async Task Get_SortedInventoryCategories_ReturnsCorrectOrder(string sortBy, string expectedFirst, string expectedSecond)
        {
            var response = await _client.GetAsync($"/api/inventory-categories?SortBy={sortBy}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryCategoryDto>>(content);

            var categories = result.Data.ToList();
            categories[0].Name.Should().Be(expectedFirst);
            categories[1].Name.Should().Be(expectedSecond);
        }

        // Pagination Tests
        [Fact]
        public async Task Get_PaginatedInventoryCategories_ReturnsCorrectPageSize()
        {
            var response = await _client.GetAsync("/api/inventory-categories?PageSize=1");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryCategoryDto>>(content);

            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(1);
        }

        [Fact]
        public async Task Get_PaginatedInventoryCategories_ReturnsSecondPage()
        {
            var response = await _client.GetAsync("/api/inventory-categories?PageSize=1&PageNumber=2");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryCategoryDto>>(content);

            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(1);
        }

        // Filtering - Exact Match
        [Fact]
        public async Task Get_FilteredInventoryCategories_ByName_ReturnsCorrectCategories()
        {
            var response = await _client.GetAsync("/api/inventory-categories?Filters[Name]=Electronics");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryCategoryDto>>(content);

            result.Data.Should().NotBeNull();
            result.Data.Should().OnlyContain(category => category.Name == "Electronics");
        }
    }
}
