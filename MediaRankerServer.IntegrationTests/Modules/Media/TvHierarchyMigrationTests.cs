using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class TvHierarchyMigrationTests
{
    private const string PreviousMigration = "20261001043450_AddEssentialsTemplates";

    [Fact]
    public async Task BackfillAndRollbackPreserveTitlesAndRestorePreviousView()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        await using var db = CreateContext(postgres.GetConnectionString());
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO media_collections (id, title, collection_type, media_type, parent_media_collection_id)
            VALUES (1001, 'Test series', 'Series', 'TvShow', NULL),
                   (1002, '10', 'Season', 'TvShow', 1001),
                   (1003, 'Unknown', 'Season', 'TvShow', 1001),
                   (1004, '999999999999999999999999', 'Season', 'TvShow', 1001);
            INSERT INTO media (id, title, media_type, media_collection_id, external_id, external_source)
            VALUES (2001, 'Test episode', 'TvShow', 1002, 'tt1234567', 'Imdb');
            INSERT INTO imdb_import_episodes (tconst, parent_tconst, season_number, episode_number, raw_line)
            VALUES ('tt1234567', 'tt1234560', 10, 12, 'migration fixture');
            """);
        await db.Database.MigrateAsync();

        Assert.Equal(10, await ScalarAsync(db, "SELECT season_number FROM media_collections WHERE id = 1002"));
        Assert.Equal(12, await ScalarAsync(db, "SELECT episode_number FROM media WHERE id = 2001"));
        Assert.Equal(2, await ScalarAsync(db, "SELECT count(*)::int FROM media_collections WHERE id IN (1003, 1004) AND season_number IS NULL"));
        Assert.False(db.Database.HasPendingModelChanges());

        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(1, await ScalarAsync(db, "SELECT count(*)::int FROM media WHERE id = 2001"));
        Assert.Equal(0, await ScalarAsync(db, "SELECT count(*)::int FROM information_schema.columns WHERE table_name = 'review_details' AND column_name = 'review_kind'"));
        Assert.Equal(1, await ScalarAsync(db, "SELECT count(*)::int FROM information_schema.columns WHERE table_name = 'reviews' AND column_name = 'media_id' AND is_nullable = 'NO'"));
    }

    [Fact]
    public async Task SeriesReviewBlocksRollbackAndTargetConstraintRejectsMissingOrDualTargets()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        await using var db = CreateContext(postgres.GetConnectionString());
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO media_collections (id, title, collection_type, media_type)
            VALUES (1001, 'Test series', 'Series', 'TvShow');
            INSERT INTO reviews (id, user_id, overall_score, media_collection_id, template_id)
            SELECT 3001, 'migration-fixture', 8, 1001, id FROM templates WHERE media_type = 'TvShow' LIMIT 1;
            """);
        Assert.Equal(1, await ScalarAsync(db, "SELECT count(*)::int FROM review_details WHERE id = 3001 AND review_kind = 'Series'"));
        var missing = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE reviews SET media_collection_id = NULL WHERE id = 3001"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, missing.SqlState);
        var dual = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE reviews SET media_id = 2001 WHERE id = 3001"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, dual.SqlState);

        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        Assert.Equal(1, await ScalarAsync(db, "SELECT count(*)::int FROM reviews WHERE id = 3001"));
        Assert.Equal(1, await ScalarAsync(db, "SELECT count(*)::int FROM review_details WHERE id = 3001 AND review_kind = 'Series'"));
    }

    private static PostgreSQLContext CreateContext(string connection) => new(
        new DbContextOptionsBuilder<PostgreSQLContext>().UseNpgsql(connection).UseSnakeCaseNamingConvention().Options);

    private static async Task<int> ScalarAsync(PostgreSQLContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
