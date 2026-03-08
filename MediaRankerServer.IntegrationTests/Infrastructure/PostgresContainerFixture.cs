using Testcontainers.PostgreSql;

namespace MediaRankerServer.IntegrationTests.Infrastructure;

public class PostgresContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("mediaranker_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.StopAsync();
    }

    public string GetConnectionString() => Container.GetConnectionString();
}
