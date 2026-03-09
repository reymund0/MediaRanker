using Xunit;

namespace MediaRankerServer.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(IntegrationTestCollection))]
public class IntegrationTestCollection : ICollectionFixture<PostgresContainerFixture>
{
}
