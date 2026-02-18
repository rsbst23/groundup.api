using Xunit;

namespace GroundUp.Tests.Integration
{
    [CollectionDefinition("IntegrationTests")]
    public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
        
        // All test classes that use [Collection("IntegrationTests")] will:
        // 1. Share a single CustomWebApplicationFactory instance
        // 2. Run sequentially (not in parallel)
        // 3. Each test will reset the database before running
    }
}
