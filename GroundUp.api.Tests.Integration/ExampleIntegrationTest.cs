using FluentAssertions;
using GroundUp.Tests.Integration;
using Newtonsoft.Json;
using System.Text;

namespace GroundUp.api.Tests.Integration
{
    public class ExampleIntegrationTest : BaseIntegrationTest
    {
        public ExampleIntegrationTest(CustomWebApplicationFactory factory) : base(factory) { }

        [Fact]
        public async Task Get_Endpoint_ReturnsSuccessAndExpectedContent()
        {
            // Arrange
            var requestUrl = "/api/users"; // Replace with your actual API route

            // Act
            var response = await _client.GetAsync(requestUrl);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode(); // Status code should be 200-299
            content.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Post_Endpoint_CreatesNewUser()
        {
            // Arrange
            var requestUrl = "/api/users"; // Replace with your actual API route
            var user = new { Name = "Test User", Email = "test@example.com" };
            var jsonContent = new StringContent(JsonConvert.SerializeObject(user), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync(requestUrl, jsonContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode(); // Should be 201 Created
            responseContent.Should().Contain("Test User");
        }
    }
}