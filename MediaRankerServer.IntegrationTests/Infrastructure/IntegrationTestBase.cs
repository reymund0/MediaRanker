using System.Net.Http;
using System.Threading.Tasks;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Xunit;

namespace MediaRankerServer.IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    private readonly string _connectionString;
    private Respawner? _respawner;
    private NpgsqlConnection? _connection;

    protected IntegrationTestBase(PostgresContainerFixture fixture)
    {
        _connectionString = fixture.GetConnectionString();
        Factory = new CustomWebApplicationFactory<Program>(_connectionString);
        Client = Factory.CreateClient();
    }

    public virtual async Task InitializeAsync()
    {
        // Apply migrations
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        await db.Database.MigrateAsync();

        // Initialize Respawner
        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();
        
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new Respawn.Graph.Table[] 
            { 
                "__EFMigrationsHistory",
                "media_types",
                "templates",
                "template_fields"
            }
        });
    }

    public virtual async Task DisposeAsync()
    {
        if (_respawner != null && _connection != null)
        {
            await _respawner.ResetAsync(_connection);
        }

        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        Client.Dispose();
        await Factory.DisposeAsync();
    }
}
