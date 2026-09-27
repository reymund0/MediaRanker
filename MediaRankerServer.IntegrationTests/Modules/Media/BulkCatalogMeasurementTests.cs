using System.Text.Json;
using FluentAssertions;
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
    public async Task FailedAdmissionFailsTheRunAndPreservesSanitizedPartialArtifact()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaRankerServer.Shared.Data.PostgreSQLContext>();
        var outputPath = Path.Combine(Path.GetTempPath(), $"bulk-measurement-failure-{Guid.NewGuid():N}.json");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_measured_admission() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'private fixture payload must not enter artifact'; END $$;
            CREATE TRIGGER reject_measured_admission BEFORE INSERT ON media
            FOR EACH ROW EXECUTE FUNCTION reject_measured_admission();
            """);
        try
        {
            var options = new BulkCatalogMeasurementOptions(outputPath, "admission", "candidate", 1, 1,
                [1], [100], ["baseline"]);
            var runner = new BulkCatalogMeasurementRunner(db.Database.GetDbConnection().ConnectionString, Client, options);
            Func<Task> run = async () => await runner.RunAsync(CancellationToken.None);

            await run.Should().ThrowAsync<Exception>();

            var json = await File.ReadAllTextAsync(outputPath);
            using var artifact = JsonDocument.Parse(json);
            artifact.RootElement.GetProperty("failureCategory").GetString().Should().NotBeNullOrWhiteSpace();
            artifact.RootElement.GetProperty("measurements").GetArrayLength().Should().Be(0);
            json.Should().NotContain("private fixture payload");
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_measured_admission ON media; DROP FUNCTION reject_measured_admission();");
            File.Delete(outputPath);
        }
    }

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
