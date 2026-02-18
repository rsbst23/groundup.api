using FluentAssertions;
using GroundUp.Core.dtos;
using Newtonsoft.Json;
using System.Text;
using Xunit;

namespace GroundUp.Tests.Integration
{
    [Collection("InventoryCategoryTests")]
    public class InventoryCategoryIntegrationTests : BaseIntegrationTest
    {
        public InventoryCategoryIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

        // Get all categories (Paginated)
        [Fact]
        public async Task Get_AllInventoryCategories_ReturnsSuccessAndCorrectData()
        {
            // Act
            var response = await _client.GetAsync("/api/inventory-categories");
            response.EnsureSuccessStatusCode();

            // Read and deserialize response
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse<PaginatedData<InventoryCategoryDto>>>(content);

            // Assertions
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.Items.Should().HaveCount(2);

            var categories = result.Data.Items.ToList();
            categories[0].Name.Should().Be("Electronics");
            categories[1].Name.Should().Be("Books");
        }


        // Get category by ID
        [Fact]
        public async Task Get_InventoryCategoryById_ReturnsCorrectCategory()
        {
            // Act
            var response = await _client.GetAsync("/api/inventory-categories/1");
            response.EnsureSuccessStatusCode();

            // Read and deserialize response
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse<InventoryCategoryDto>>(content);

            // Assertions
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.Name.Should().Be("Electronics");
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

            // Act
            var response = await _client.PostAsync("/api/inventory-categories", jsonContent);
            response.EnsureSuccessStatusCode();

            // Read and deserialize response
            var content = await response.Content.ReadAsStringAsync();
            var createdCategory = JsonConvert.DeserializeObject<ApiResponse<InventoryCategoryDto>>(content);

            // Assertions
            createdCategory.Should().NotBeNull();
            createdCategory.Success.Should().BeTrue();
            createdCategory.Data.Should().NotBeNull();
            createdCategory.Data.Name.Should().Be("Clothing");
        }


        // Update existing category
        [Fact]
        public async Task Put_UpdateInventoryCategory_ReturnsUpdatedCategory()
        {
            var updatedCategory = new
            {
                Id = 1,
                Name = "Updated Electronics"
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(updatedCategory), Encoding.UTF8, "application/json");

            // Act: Send the PUT request
            var response = await _client.PutAsync("/api/inventory-categories/1", jsonContent);
            response.EnsureSuccessStatusCode();

            // Read and deserialize response
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse<InventoryCategoryDto>>(content);

            // Assertions
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.Name.Should().Be("Updated Electronics");

            // Verify update via GET request
            var getResponse = await _client.GetAsync("/api/inventory-categories/1");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var getResult = JsonConvert.DeserializeObject<ApiResponse<InventoryCategoryDto>>(getContent);

            getResult.Should().NotBeNull();
            getResult.Success.Should().BeTrue();
            getResult.Data.Should().NotBeNull();
            getResult.Data.Name.Should().Be("Updated Electronics");
        }

        // Delete category
        [Fact]
        public async Task Delete_InventoryCategory_ReturnsSuccessAndRemovesCategory()
        {
            // Act: Send DELETE request
            var response = await _client.DeleteAsync("/api/inventory-categories/1");
            response.EnsureSuccessStatusCode();

            // Read and deserialize response
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse<bool>>(content);

            // Assertions
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().BeTrue();

            // Verify deletion via GET request
            var getResponse = await _client.GetAsync("/api/inventory-categories/1");
            getResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }


        // Sorting Tests
        [Theory]
        [InlineData("Name", "Books", "Electronics")]  // Ascending order
        [InlineData("-Name", "Electronics", "Books")] // Descending order
        public async Task Get_SortedInventoryCategories_ReturnsCorrectOrder(string sortBy, string expectedFirst, string expectedSecond)
        {
            // Act: Send GET request with sorting
            var response = await _client.GetAsync($"/api/inventory-categories?SortBy={sortBy}");
            response.EnsureSuccessStatusCode();

            // Read and deserialize response
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse<PaginatedData<InventoryCategoryDto>>>(content);

            // Assertions
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.Items.Should().HaveCount(2); // Ensure correct data length

            var categories = result.Data.Items.ToList();
            categories[0].Name.Should().Be(expectedFirst);
            categories[1].Name.Should().Be(expectedSecond);
        }

        // Pagination Tests
        [Fact]
        public async Task Get_PaginatedInventoryCategories_ReturnsCorrectPageSize()
        {
            // Act: Send GET request with pagination
            var response = await _client.GetAsync("/api/inventory-categories?PageSize=1");
            response.EnsureSuccessStatusCode();

            // Read and deserialize response
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse<PaginatedData<InventoryCategoryDto>>>(content);

            // Assertions
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.Items.Should().HaveCount(1); // Ensure page size is 1
        }

        [Fact]
        public async Task Get_PaginatedInventoryCategories_ReturnsSecondPage()
        {
            // Act: Send GET request with PageSize and PageNumber for second page
            var response = await _client.GetAsync("/api/inventory-categories?PageSize=1&PageNumber=2");
            response.EnsureSuccessStatusCode();

            // Read and deserialize response
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse<PaginatedData<InventoryCategoryDto>>>(content);

            // Assertions
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.Items.Should().HaveCount(1); // Ensure page size is 1
            result.Data.PageNumber.Should().Be(2); // Verify the correct page is returned
        }

        // Filtering - Exact Match
        [Fact]
        public async Task Get_FilteredInventoryCategories_ByName_ReturnsCorrectCategories()
        {
            // Act: Send GET request with filtering by name
            var response = await _client.GetAsync("/api/inventory-categories?Filters[Name]=Electronics");
            response.EnsureSuccessStatusCode();

            // Read and deserialize response
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse<PaginatedData<InventoryCategoryDto>>>(content);

            // Assertions
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.Items.Should().NotBeEmpty();
            result.Data.Items.Should().OnlyContain(category => category.Name == "Electronics");
        }
    }
}
