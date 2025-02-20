using Xunit;

namespace GroundUp.Tests.Integration
{
    [CollectionDefinition("DatabaseTests")]
    public class DatabaseTestCollection : ICollectionFixture<CustomWebApplicationFactory>
    {
        // Ensures all tests in this collection run sequentially
    }
}