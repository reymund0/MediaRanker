using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class MediaTypeMigrationCompatibilityTests
{
    private const string BeforeRemoveMediaTypes = "20260507155216_AddImdbImportRatings";
    private const string RemoveMediaTypes = "20260520193015_RemoveMediaTypesTable";
    private const string AutomaticCoverArt = "20260921194619_AutomaticCoverArt";

    private static readonly string[] CanonicalMediaTypes =
        ["VideoGame", "Book", "Movie", "TvShow", "Album", "Concert"];

    [Fact]
    public async Task FreshMigrationPreservesAllTypesAndRollbackRestoresIdsAndViewShape()
    {
        await using var postgres = await StartPostgresAsync();
        await using var db = CreateContext(postgres.GetConnectionString());
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(BeforeRemoveMediaTypes);
        await SeedLegacyRowsAsync(db);

        await migrator.MigrateAsync(RemoveMediaTypes);

        Assert.Equal(CanonicalMediaTypes, await ReadStringsAsync(
            db, "SELECT media_type FROM media WHERE id BETWEEN 2001 AND 2006 ORDER BY id"));
        Assert.Equal(CanonicalMediaTypes, await ReadStringsAsync(
            db, "SELECT media_type FROM media_collections WHERE id BETWEEN 3001 AND 3006 ORDER BY id"));
        Assert.Equal(CanonicalMediaTypes, await ReadStringsAsync(
            db, "SELECT media_type FROM templates WHERE id BETWEEN 1001 AND 1006 ORDER BY id"));
        Assert.Equal("covers/migration.webp", await ReadStringAsync(
            db, "SELECT media_cover_file_key FROM review_details WHERE id = 7001"));
        Assert.Equal(1L, await ReadLongAsync(db, "SELECT count(*) FROM reviews WHERE id = 7001"));

        await migrator.MigrateAsync(BeforeRemoveMediaTypes);

        Assert.Equal(Enumerable.Range(1, 6).Select(value => (long)-value), await ReadLongsAsync(
            db, "SELECT media_type_id FROM media WHERE id BETWEEN 2001 AND 2006 ORDER BY id"));
        Assert.Equal(Enumerable.Range(1, 6).Select(value => (long)-value), await ReadLongsAsync(
            db, "SELECT media_type_id FROM media_collections WHERE id BETWEEN 3001 AND 3006 ORDER BY id"));
        Assert.Equal(Enumerable.Range(1, 6).Select(value => (long)-value), await ReadLongsAsync(
            db, "SELECT media_type_id FROM templates WHERE id BETWEEN 1001 AND 1006 ORDER BY id"));
        Assert.Equal(1L, await ReadLongAsync(db, "SELECT count(*) FROM reviews WHERE id = 7001"));
        Assert.Equal("covers/migration.webp", await ReadStringAsync(
            db, "SELECT media_cover_file_key FROM review_details WHERE id = 7001"));

        var rollbackColumns = await ReadStringsAsync(db, """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = 'review_details'
            ORDER BY ordinal_position
            """);
        Assert.Contains("media_type_id", rollbackColumns);
        Assert.Contains("media_type_name", rollbackColumns);
        Assert.Contains("media_cover_file_key", rollbackColumns);
        var duplicateName = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("INSERT INTO media_types (id, name) VALUES (99, 'Movie')"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicateName.SqlState);
        Assert.Equal("uq_media_types_name", duplicateName.ConstraintName);
    }

    [Fact]
    public async Task MigrationRejectsAnUnmappedTypeWithoutDroppingItsRows()
    {
        await using var postgres = await StartPostgresAsync();
        await using var db = CreateContext(postgres.GetConnectionString());
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(BeforeRemoveMediaTypes);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO media_types (id, name) VALUES (1, 'Custom');
            INSERT INTO media (id, title, release_date, media_type_id)
            VALUES (9001, 'Custom title', DATE '2020-01-01', 1);
            """);

        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(RemoveMediaTypes));

        Assert.Equal("Custom", await ReadStringAsync(db, "SELECT name FROM media_types WHERE id = 1"));
        Assert.Equal(1L, await ReadLongAsync(db, "SELECT count(*) FROM media WHERE id = 9001"));
        Assert.DoesNotContain(RemoveMediaTypes, await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task PendingMigrationAfterAutomaticCoverArtPreservesCoverIdentityFreshnessAndReviewLinks()
    {
        await using var postgres = await StartPostgresAsync();
        await using var db = CreateContext(postgres.GetConnectionString());
        var migrator = db.GetService<IMigrator>();

        await db.Database.MigrateAsync();
        await SeedAutomaticCoverRowsAsync(db);

        var coversBefore = await ReadCoverSnapshotsAsync(db);
        Assert.Equal(901L, await ReadLongAsync(db, "SELECT media_cover_id FROM review_details WHERE id = 8001"));

        // Revert only MR-51 in this isolated database so its already-applied AutoArt schema stays in place.
        var migrationDownScript = migrator.GenerateScript(RemoveMediaTypes, BeforeRemoveMediaTypes);
        await ExecuteScriptAsync(db, migrationDownScript);

        Assert.Equal(901L, await ReadLongAsync(db, "SELECT media_cover_id FROM review_details WHERE id = 8001"));
        var rollbackViewColumns = await ReadStringsAsync(db, """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = 'review_details'
            """);
        Assert.Contains("media_cover_id", rollbackViewColumns);
        Assert.Contains("media_type_id", rollbackViewColumns);
        Assert.Contains("media_type_name", rollbackViewColumns);

        await db.Database.MigrateAsync();

        Assert.Equal(coversBefore, await ReadCoverSnapshotsAsync(db));
        Assert.Equal(901L, await ReadLongAsync(db, "SELECT media_cover_id FROM review_details WHERE id = 8001"));
        Assert.Equal("TvShow", await ReadStringAsync(db, "SELECT media_type FROM review_details WHERE id = 8001"));
        Assert.Equal(1L, await ReadLongAsync(db, "SELECT count(*) FROM reviews WHERE id = 8001"));
        Assert.Contains(AutomaticCoverArt, await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task AutomaticCoverArtRollbackSupportsLegacyHistoryWithPendingMediaTypeMigration()
    {
        await using var postgres = await StartPostgresAsync();
        await using var db = CreateContext(postgres.GetConnectionString());
        var migrator = db.GetService<IMigrator>();
        await db.Database.MigrateAsync();
        await SeedAutomaticCoverRowsAsync(db);

        await ExecuteScriptAsync(db, migrator.GenerateScript(RemoveMediaTypes, BeforeRemoveMediaTypes));
        await ExecuteScriptAsync(db, migrator.GenerateScript(AutomaticCoverArt, RemoveMediaTypes));

        Assert.Equal(-4L, await ReadLongAsync(db, "SELECT media_type_id FROM review_details WHERE id = 8001"));
        Assert.Equal("TV Show", await ReadStringAsync(db, "SELECT media_type_name FROM review_details WHERE id = 8001"));
        Assert.Equal(1L, await ReadLongAsync(db, "SELECT count(*) FROM reviews WHERE id = 8001"));
        Assert.Equal(1L, await ReadLongAsync(db, """
            SELECT count(*) FROM review_details WHERE id = 8001 AND media_cover_file_key IS NULL
            """));
    }

    private static async Task<PostgreSqlContainer> StartPostgresAsync()
    {
        var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("migration_compatibility")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await postgres.StartAsync();
        return postgres;
    }

    private static PostgreSQLContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PostgreSQLContext(options);
    }

    private static Task SeedLegacyRowsAsync(PostgreSQLContext db) => db.Database.ExecuteSqlRawAsync("""
        INSERT INTO media_covers
            (id, file_upload_id, file_key, file_name, file_size_bytes, file_content_type)
        VALUES
            (9001, 9001, 'covers/migration.webp', 'migration.webp', 12, 'image/webp');

        INSERT INTO templates (id, user_id, name, media_type_id)
        VALUES
            (1001, 'migration-test', 'Template VideoGame', -1),
            (1002, 'migration-test', 'Template Book', -2),
            (1003, 'migration-test', 'Template Movie', -3),
            (1004, 'migration-test', 'Template TvShow', -4),
            (1005, 'migration-test', 'Template Album', -5),
            (1006, 'migration-test', 'Template Concert', -6);

        INSERT INTO media_collections (id, title, collection_type, release_date, media_type_id)
        VALUES
            (3001, 'Collection VideoGame', 'Series', DATE '2020-01-01', -1),
            (3002, 'Collection Book', 'Series', DATE '2020-01-01', -2),
            (3003, 'Collection Movie', 'Series', DATE '2020-01-01', -3),
            (3004, 'Collection TvShow', 'Series', DATE '2020-01-01', -4),
            (3005, 'Collection Album', 'Series', DATE '2020-01-01', -5),
            (3006, 'Collection Concert', 'Series', DATE '2020-01-01', -6);

        INSERT INTO media (id, title, release_date, media_type_id, media_collection_id, cover_id)
        VALUES
            (2001, 'Media VideoGame', DATE '2020-01-01', -1, 3001, 9001),
            (2002, 'Media Book', DATE '2020-01-01', -2, 3002, NULL),
            (2003, 'Media Movie', DATE '2020-01-01', -3, 3003, NULL),
            (2004, 'Media TvShow', DATE '2020-01-01', -4, 3004, NULL),
            (2005, 'Media Album', DATE '2020-01-01', -5, 3005, NULL),
            (2006, 'Media Concert', DATE '2020-01-01', -6, 3006, NULL);

        INSERT INTO reviews (id, user_id, overall_score, media_id, template_id)
        VALUES (7001, 'migration-test', 8, 2001, 1001);
        """);

    private static Task SeedAutomaticCoverRowsAsync(PostgreSQLContext db) => db.Database.ExecuteSqlRawAsync("""
        INSERT INTO media_covers
            (id, provider, lookup_kind, lookup_id, provider_item_id, image_path, outcome,
             checked_at, expires_at, attempt_count, version)
        VALUES
            (901, 'Tmdb', 'SeriesImdb', 'tt-series', 'tmdb-series', '/series.jpg', 'Ready',
             TIMESTAMPTZ '2026-09-01T00:00:00Z', TIMESTAMPTZ '2026-10-01T00:00:00Z', 2, 17),
            (902, 'Tmdb', 'MovieImdb', 'tt-episode', 'tmdb-movie', '/direct.jpg', 'Ready',
             TIMESTAMPTZ '2026-09-02T00:00:00Z', TIMESTAMPTZ '2026-10-02T00:00:00Z', 3, 19);

        INSERT INTO media_collections
            (id, title, collection_type, release_date, media_type, cover_id)
        VALUES
            (701, 'Series', 'Series', DATE '2020-01-01', 'TvShow', 901),
            (702, 'Season', 'Season', DATE '2020-01-01', 'TvShow', NULL);
        UPDATE media_collections SET parent_media_collection_id = 701 WHERE id = 702;

        INSERT INTO media (id, title, release_date, external_id, external_source, media_type,
                           media_collection_id, cover_id)
        VALUES
            (801, 'Episode', DATE '2020-01-01', 'tt-episode', 'Imdb', 'TvShow', 702, 902);

        INSERT INTO reviews (id, user_id, overall_score, media_id, template_id)
        VALUES (8001, 'migration-test', 9, 801, -1);
        """);

    private static async Task ExecuteScriptAsync(PostgreSQLContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> ReadStringsAsync(PostgreSQLContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    private static async Task<string> ReadStringAsync(PostgreSQLContext db, string sql) =>
        (await ReadStringsAsync(db, sql)).Single();

    private static async Task<List<long>> ReadLongsAsync(PostgreSQLContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<long>();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetInt64(0));
        }
        return result;
    }

    private static async Task<long> ReadLongAsync(PostgreSQLContext db, string sql) =>
        (await ReadLongsAsync(db, sql)).Single();

    private static async Task<List<CoverSnapshot>> ReadCoverSnapshotsAsync(PostgreSQLContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT id, lookup_id, image_path, outcome, checked_at, expires_at, version
            FROM media_covers
            WHERE id IN (901, 902)
            ORDER BY id
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<CoverSnapshot>();
        while (await reader.ReadAsync())
        {
            result.Add(new CoverSnapshot(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetInt64(6)));
        }
        return result;
    }

    private sealed record CoverSnapshot(
        long Id,
        string LookupId,
        string ImagePath,
        string Outcome,
        DateTimeOffset CheckedAt,
        DateTimeOffset ExpiresAt,
        long Version);
}
