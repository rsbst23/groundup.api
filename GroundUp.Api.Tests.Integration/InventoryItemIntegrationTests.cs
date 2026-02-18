using FluentAssertions;
using GroundUp.Core.dtos;
using Newtonsoft.Json;
using System.Text;
using Xunit;

namespace GroundUp.Tests.Integration
{
    [Collection("InventoryItemTests")]
    public class InventoryItemIntegrationTests : BaseIntegrationTest
    {
        public InventoryItemIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

        //[Fact]
        //public async Task Get_AllInventoryItems_ReturnsSuccessAndCorrectData()
        //{
        //    var response = await _client.GetAsync("/api/inventory-items");
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryItemDto>>(content);

        //    result.Data.Should().NotBeNull();
        //    result.Data.Should().HaveCount(2);

        //    var items = result.Data.ToList();
        //    items[0].Name.Should().Be("Laptop");
        //    items[1].Name.Should().Be("The Great Gatsby");
        //}

        //// Get item by ID (NEWLY ADDED)
        //[Fact]
        //public async Task Get_InventoryItemById_ReturnsCorrectItem()
        //{
        //    var response = await _client.GetAsync("/api/inventory-items/1");
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<InventoryItemDto>(content);

        //    result.Should().NotBeNull();
        //    result.Name.Should().Be("Laptop");
        //}

        //[Fact]
        //public async Task Post_CreateInventoryItem_ReturnsCreatedItem()
        //{
        //    var newItem = new
        //    {
        //        Name = "Smartphone",
        //        PurchasePrice = 599.99m,
        //        Condition = "New",
        //        InventoryCategoryId = 1,
        //        PurchaseDate = "2024-02-20T00:00:00Z"
        //    };

        //    var jsonContent = new StringContent(JsonConvert.SerializeObject(newItem), Encoding.UTF8, "application/json");

        //    var response = await _client.PostAsync("/api/inventory-items", jsonContent);
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var createdItem = JsonConvert.DeserializeObject<InventoryItemDto>(content);

        //    createdItem.Should().NotBeNull();
        //    createdItem.Name.Should().Be("Smartphone");
        //}

        //// Update existing item (NEWLY ADDED)
        //[Fact]
        //public async Task Put_UpdateInventoryItem_ReturnsNoContent()
        //{
        //    var updatedItem = new
        //    {
        //        Id = 1,
        //        Name = "Updated Laptop",
        //        PurchasePrice = 1099.99m,
        //        Condition = "Like New",
        //        InventoryCategoryId = 1,
        //        PurchaseDate = "2024-02-21T00:00:00Z"
        //    };

        //    var jsonContent = new StringContent(JsonConvert.SerializeObject(updatedItem), Encoding.UTF8, "application/json");

        //    var response = await _client.PutAsync("/api/inventory-items/1", jsonContent);
        //    response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        //    // Verify the update
        //    var getResponse = await _client.GetAsync("/api/inventory-items/1");
        //    var getContent = await getResponse.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<InventoryItemDto>(getContent);

        //    result.Name.Should().Be("Updated Laptop");
        //}

        //[Fact]
        //public async Task Delete_InventoryItem_ReturnsNoContentAndRemovesItem()
        //{
        //    var response = await _client.DeleteAsync("/api/inventory-items/1");
        //    response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        //    var getResponse = await _client.GetAsync("/api/inventory-items/1");
        //    getResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        //}

        //// Sorting Tests
        //[Theory]
        //[InlineData("Name", "Laptop", "The Great Gatsby")]  // Ascending
        //[InlineData("-Name", "The Great Gatsby", "Laptop")] // Descending
        //public async Task Get_SortedInventoryItems_ReturnsCorrectOrder(string sortBy, string expectedFirst, string expectedSecond)
        //{
        //    var response = await _client.GetAsync($"/api/inventory-items?SortBy={sortBy}");
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryItemDto>>(content);

        //    var items = result.Data.ToList();
        //    items[0].Name.Should().Be(expectedFirst);
        //    items[1].Name.Should().Be(expectedSecond);
        //}

        //// Pagination Tests
        //[Fact]
        //public async Task Get_PaginatedInventoryItems_ReturnsCorrectPageSize()
        //{
        //    var response = await _client.GetAsync("/api/inventory-items?PageSize=1");
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryItemDto>>(content);

        //    result.Data.Should().NotBeNull();
        //    result.Data.Should().HaveCount(1);
        //}

        //[Fact]
        //public async Task Get_PaginatedInventoryItems_ReturnsSecondPage()
        //{
        //    var response = await _client.GetAsync("/api/inventory-items?PageSize=1&PageNumber=2");
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryItemDto>>(content);

        //    result.Data.Should().NotBeNull();
        //    result.Data.Should().HaveCount(1);
        //}

        //// Filtering Tests - Exact Match
        //[Fact]
        //public async Task Get_FilteredInventoryItems_ByCondition_ReturnsCorrectItems()
        //{
        //    var response = await _client.GetAsync("/api/inventory-items?Filters[Condition]=New");
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryItemDto>>(content);

        //    result.Data.Should().NotBeNull();
        //    result.Data.Should().OnlyContain(item => item.Condition == "New");
        //}

        //// Filtering Tests - Range Filters
        //[Fact]
        //public async Task Get_FilteredInventoryItems_ByPriceRange_ReturnsCorrectItems()
        //{
        //    var response = await _client.GetAsync("/api/inventory-items?MinFilters[PurchasePrice]=500");
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryItemDto>>(content);

        //    result.Data.Should().NotBeNull();
        //    result.Data.Should().OnlyContain(item => item.PurchasePrice >= 500);
        //}

        //// Filtering Tests - Multi-Value Filters (IN Operator)
        //[Fact]
        //public async Task Get_FilteredInventoryItems_ByMultipleConditions_ReturnsCorrectItems()
        //{
        //    var response = await _client.GetAsync("/api/inventory-items?MultiValueFilters[Condition]=New,Used");
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryItemDto>>(content);

        //    result.Data.Should().NotBeNull();
        //    result.Data.Should().OnlyContain(item => item.Condition == "New" || item.Condition == "Used");
        //}

        //// Filtering Tests - Date Range Filters
        //[Fact]
        //public async Task Get_FilteredInventoryItems_ByDateRange_ReturnsCorrectItems()
        //{
        //    var response = await _client.GetAsync($"/api/inventory-items?MinFilters[PurchaseDate]={DateTime.UtcNow.AddDays(-1):yyyy-MM-dd}");
        //    response.EnsureSuccessStatusCode();

        //    var content = await response.Content.ReadAsStringAsync();
        //    var result = JsonConvert.DeserializeObject<PaginatedResponse<InventoryItemDto>>(content);

        //    result.Data.Should().NotBeNull();
        //    result.Data.Should().OnlyContain(item => item.PurchaseDate >= DateTime.UtcNow.AddDays(-1));
        //}
    }
}
