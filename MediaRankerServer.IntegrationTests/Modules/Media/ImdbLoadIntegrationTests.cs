using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class ImdbLoadIntegrationTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    // Seed rows: inserted via EF directly into imdb_imports table.
    // (a) eligible movie with year
    private const string TconstMovieWithYear   = "tt0000001";
    // (b) eligible movie with null year
    private const string TconstMovieNullYear   = "tt0000002";
    // (c) eligible video game
    private const string TconstVideoGame       = "tt0000003";
    // (d) ineligible tvSeries
    private const string TconstTvSeries        = "tt0000004";
    // (e) duplicate of (a) with mutated title — used in idempotency run
    private const string TconstDuplicateMovie  = TconstMovieWithYear;

    private async Task SeedImdbImportsAsync(bool withMutatedDuplicate = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        var rows = new List<ImdbImport>
        {
            new() { Tconst = TconstMovieWithYear,  TitleType = "movie",    PrimaryTitle = "Original Title",  OriginalTitle = "Original Title",  StartYear = 2001, RawLine = "a" },
            new() { Tconst = TconstMovieNullYear,  TitleType = "movie",    PrimaryTitle = "No Year Movie",   OriginalTitle = "No Year Movie",   StartYear = null, RawLine = "b" },
            new() { Tconst = TconstVideoGame,      TitleType = "videoGame",PrimaryTitle = "Cool Game",       OriginalTitle = "Cool Game",       StartYear = 2005, RawLine = "c" },
            new() { Tconst = TconstTvSeries,       TitleType = "tvSeries", PrimaryTitle = "Some Series",     OriginalTitle = "Some Series",     StartYear = 2010, RawLine = "d" },
        };

        if (withMutatedDuplicate)
        {
            // Remove the original row for (a) and replace with mutated title — simulates a second import run
            // with updated data. Since tconst is unique, we use ExecuteSqlRaw to update in-place.
            await db.Database.ExecuteSqlRawAsync($"""
                UPDATE imdb_imports SET primary_title = 'Updated Title' WHERE tconst = '{TconstMovieWithYear}'
                """);
            return;
        }

        foreach (var row in rows)
        {
            // Avoid duplicate constraint if test is retried
            var exists = await db.ImdbImports.AnyAsync(i => i.Tconst == row.Tconst);
            if (!exists) db.ImdbImports.Add(row);
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task LoadAsync_InsertsEligibleRowsOnly()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();

        var result = await loadService.LoadAsync();

        result.Affected.Should().Be(3, "movie-with-year, movie-null-year, and videoGame are eligible; tvSeries is not");

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var mediaRows = await db.Media.ToListAsync();

        mediaRows.Should().HaveCount(3);
        mediaRows.Should().NotContain(m => m.ExternalId == TconstTvSeries);
    }

    [Fact]
    public async Task LoadAsync_MovieWithYear_HasMidYearReleaseDate()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var row = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == TconstMovieWithYear);

        row.Should().NotBeNull();
        row!.ReleaseDate.Should().Be(new DateOnly(2001, 7, 1));
    }

    [Fact]
    public async Task LoadAsync_MovieWithNullYear_HasNullReleaseDate()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var row = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == TconstMovieNullYear);

        row.Should().NotBeNull();
        row!.ReleaseDate.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_VideoGame_HasVideoGameMediaTypeId()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var row = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == TconstVideoGame);

        row.Should().NotBeNull();
        row!.MediaTypeId.Should().Be(-1L);
    }

    [Fact]
    public async Task LoadAsync_SetsExternalSourceToImdb()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var rows = await db.Media.Where(m => m.ExternalSource == MediaExternalSource.Imdb).ToListAsync();

        rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task LoadAsync_IsIdempotent_AndUpdatesTitle()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();

        // First run
        await loadService.LoadAsync();

        // Mutate the title of row (a) in imdb_imports
        await SeedImdbImportsAsync(withMutatedDuplicate: true);

        // Second run — should DO UPDATE, not INSERT new row
        var result = await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var allMedia = await db.Media.ToListAsync();
        allMedia.Should().HaveCount(3, "no new rows should be inserted on second run");

        var updatedRow = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == TconstMovieWithYear);
        updatedRow.Should().NotBeNull();
        updatedRow!.Title.Should().Be("Updated Title");
    }
}
