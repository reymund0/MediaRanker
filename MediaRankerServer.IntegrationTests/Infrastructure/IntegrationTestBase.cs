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
        
        // Clean up any leaked data from previous runs before setting up Respawner
        await ResetMutableDataAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = 
            [ 
                "__EFMigrationsHistory",
                "media_types",
                "templates",
                "template_fields"
            ]
        });
    }

    public virtual async Task DisposeAsync()
    {
        if (_respawner != null && _connection != null)
        {
            await _respawner.ResetAsync(_connection);
        }

        // Clean up seeded tables with mixed data (preserving negative IDs)
        await ResetMutableDataAsync();

        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        Client.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>
    /// Manually deletes user-created rows (ID > 0) from tables that contain both system-seeded data 
    /// (negative IDs) and test data. This supplements Respawn which is configured to ignore these tables.
    /// </summary>
    private async Task ResetMutableDataAsync()
    {
        if (_connection == null) return;

        var sql = @"
            DELETE FROM ranked_media_scores WHERE ranked_media_id > 0 OR template_field_id > 0;
            DELETE FROM ranked_media WHERE id > 0;
            DELETE FROM template_fields WHERE id > 0;
            DELETE FROM templates WHERE id > 0;
            DELETE FROM media WHERE id > 0;
        ";

        using var cmd = new NpgsqlCommand(sql, _connection);
        await cmd.ExecuteNonQueryAsync();
    }
}
