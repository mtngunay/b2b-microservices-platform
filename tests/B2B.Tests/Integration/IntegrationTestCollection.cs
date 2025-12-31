using B2B.Tests.Integration.Fixtures;

namespace B2B.Tests.Integration;

/// <summary>
/// Collection definition for integration tests.
/// Tests in this collection share the same WebApplicationFactory instance.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<B2BWebApplicationFactory>
{
}
