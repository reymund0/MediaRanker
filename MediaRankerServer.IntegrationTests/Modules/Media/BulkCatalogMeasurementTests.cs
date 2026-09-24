using System.Text.Json;
using MediaRankerServer.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

/// <summary>
/// Focused, opt-in baseline/candidate measurement entry point for OpenSpec tasks
/// 1.2 and 1.3. Ordinary test runs intentionally do no benchmark work.
/// </summary>
public sealed class BulkCatalogMeasurementTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task OptInMeasurement_WritesSanitizedArtifact()
    {
        var options = BulkCatalogMeasurementOptions.FromEnvironment();
        if (options is null)
            return;

        // Resolve the fixture-owned connection string from the test application's context.
        // This is deliberately not the original application's configured connection.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaRankerServer.Shared.Data.PostgreSQLContext>();
        var connectionString = db.Database.GetDbConnection().ConnectionString;

        var runner = new BulkCatalogMeasurementRunner(connectionString, Client, options);
        var artifact = await runner.RunAsync(CancellationToken.None);

        var directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(options.OutputPath, JsonSerializer.Serialize(artifact, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }), CancellationToken.None);
    }
}
