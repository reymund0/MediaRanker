using System.Net;
using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class MediaShowcaseTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task Showcase_IsAnonymousCachedAndIncludesOnlyReadyCoversInExactShape()
    {
        var now = AtTodayUtc(9);
        using var anonymousFactory = CreateAnonymousFactory(now);
        using var anonymousClient = anonymousFactory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        for (var i = 0; i < 22; i++)
        {
            var cover = CreateReadyCover($"showcase-{i}", $"/showcase-{i}.jpg", now.AddDays(3));
            db.Media.Add(new MediaEntity
            {
                Title = $"Ready showcase {i}",
                MediaType = "Movie",
                Cover = cover
            });
        }

        var expiredCover = CreateReadyCover("showcase-expired", "/expired.jpg", now.AddDays(-1));
        db.Media.Add(new MediaEntity { Title = "Expired showcase", MediaType = "Movie", Cover = expiredCover });

        var pendingCover = new MediaCover
        {
            Provider = ArtworkProvider.Tmdb,
            LookupKind = CoverLookupKind.MovieImdb,
            LookupId = "showcase-pending",
            Outcome = CoverOutcome.Pending
        };
        db.Media.Add(new MediaEntity { Title = "Pending showcase", MediaType = "Movie", Cover = pendingCover });
        db.Media.Add(new MediaEntity
        {
            Title = "Unresolved showcase",
            MediaType = "Movie",
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt9999999"
        });
        await db.SaveChangesAsync();

        var coversBeforeRequests = await db.MediaCovers.CountAsync();
        using var response = await anonymousClient.GetAsync("/api/media/showcase");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstJson = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(firstJson);
        var items = document.RootElement;
        items.ValueKind.Should().Be(JsonValueKind.Array);
        items.GetArrayLength().Should().Be(20);
        foreach (var item in items.EnumerateArray())
            item.EnumerateObject().Select(property => property.Name).Should().Equal("title", "coverImageUrl");

        var showcaseItems = JsonSerializer.Deserialize<List<ShowcaseMediaDto>>(
            firstJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        showcaseItems.Should().OnlyContain(item =>
            item.Title.StartsWith("Ready showcase ", StringComparison.Ordinal)
            && item.CoverImageUrl.StartsWith("https://image.tmdb.org/t/p/w342/showcase-", StringComparison.Ordinal));
        showcaseItems.Select(item => item.Title).Should().NotContain("Expired showcase", "Pending showcase", "Unresolved showcase");

        var selectedMedia = db.Media.Local.Single(media => media.Title == showcaseItems[0].Title);
        selectedMedia.Title = "Renamed after first showcase response";
        await db.SaveChangesAsync();

        using var secondResponse = await anonymousClient.GetAsync("/api/media/showcase");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondJson = await secondResponse.Content.ReadAsStringAsync();
        secondJson.Should().Be(firstJson, "the first request's daily result should remain cached after catalog rows change");

        (await db.MediaCovers.CountAsync()).Should().Be(coversBeforeRequests);
        (await db.MediaCovers.SingleAsync(cover => cover.LookupId == "showcase-pending")).RequestedAt.Should().BeNull();
    }

    [Fact]
    public async Task Showcase_ExcludesIntradayExpiryAndUnknownSeasonsAndCachesFewerThanTwentyReadyItems()
    {
        var now = AtTodayUtc(9);
        using var anonymousFactory = CreateAnonymousFactory(now);
        using var anonymousClient = anonymousFactory.CreateClient();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        for (var i = 0; i < 2; i++)
        {
            var cover = CreateReadyCover($"fewer-{i}", $"/fewer-{i}.jpg", now.AddDays(2));
            db.Media.Add(new MediaEntity { Title = $"Durable showcase {i}", MediaType = "Movie", Cover = cover });
        }
        var intradayCover = CreateReadyCover("showcase-intraday", "/intraday.jpg", now.AddHours(6));
        db.Media.Add(new MediaEntity { Title = "Intraday expiry showcase", MediaType = "Movie", Cover = intradayCover });
        db.Media.Add(new MediaEntity
        {
            Title = "Hidden unknown season showcase",
            MediaType = "TvShow",
            Cover = CreateReadyCover("showcase-unknown-season", "/unknown-season.jpg", now.AddDays(2)),
            MediaCollection = new MediaCollection
            {
                Title = "Unknown",
                MediaType = "TvShow",
                CollectionType = MediaCollectionType.Season,
                ParentMediaCollection = new MediaCollection
                {
                    Title = "Unknown season showcase series",
                    MediaType = "TvShow",
                    CollectionType = MediaCollectionType.Series
                }
            }
        });
        await db.SaveChangesAsync();

        using var firstResponse = await anonymousClient.GetAsync("/api/media/showcase");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstJson = await firstResponse.Content.ReadAsStringAsync();
        var firstItems = JsonSerializer.Deserialize<List<ShowcaseMediaDto>>(
            firstJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        firstItems.Select(item => item.Title).Should().BeEquivalentTo("Durable showcase 0", "Durable showcase 1");
        firstItems.Should().NotContain(item => item.Title == "Intraday expiry showcase");

        db.Media.Add(new MediaEntity
        {
            Title = "Added after showcase cache",
            MediaType = "Movie",
            Cover = CreateReadyCover("showcase-after-cache", "/after-cache.jpg", now.AddDays(2))
        });
        await db.SaveChangesAsync();

        using var secondResponse = await anonymousClient.GetAsync("/api/media/showcase");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await secondResponse.Content.ReadAsStringAsync()).Should().Be(firstJson);
    }

    [Fact]
    public async Task Showcase_CachesEmptyResultWhenNoCoversAreReady()
    {
        var now = AtTodayUtc(9);
        using var anonymousFactory = CreateAnonymousFactory(now);
        using var anonymousClient = anonymousFactory.CreateClient();
        using var firstResponse = await anonymousClient.GetAsync("/api/media/showcase");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstJson = await firstResponse.Content.ReadAsStringAsync();
        firstJson.Should().Be("[]");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Media.Add(new MediaEntity
        {
            Title = "Ready after empty showcase cache",
            MediaType = "Movie",
            Cover = CreateReadyCover("showcase-after-empty-cache", "/after-empty-cache.jpg", now.AddDays(2))
        });
        await db.SaveChangesAsync();

        using var secondResponse = await anonymousClient.GetAsync("/api/media/showcase");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await secondResponse.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task Showcase_ConcurrentColdRequestsShareFirstResultAfterCatalogChanges()
    {
        var now = AtTodayUtc(9);
        var interceptor = new ShowcaseReadInterceptor();
        var cache = new ObservingMemoryCache(interceptor.FirstRead);
        using var anonymousFactory = CreateAnonymousFactory(now, cache, interceptor);
        using var anonymousClient = anonymousFactory.CreateClient();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var media = new MediaEntity
        {
            Title = "Original concurrent showcase",
            MediaType = "Movie",
            Cover = CreateReadyCover("showcase-concurrent", "/concurrent.jpg", now.AddDays(2))
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();

        var firstResponseTask = anonymousClient.GetAsync("/api/media/showcase");
        Task<HttpResponseMessage> secondResponseTask;
        try
        {
            // Hold A after its SQL snapshot, then force B to miss the still-empty cache.
            await interceptor.FirstRead.WaitAsync(TimeSpan.FromSeconds(10));
            media.Title = "Changed between concurrent reads";
            await db.SaveChangesAsync();
            secondResponseTask = anonymousClient.GetAsync("/api/media/showcase");
            await cache.ConcurrentMiss.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            interceptor.ReleaseFirstRead();
        }

        using var firstResponse = await firstResponseTask.WaitAsync(TimeSpan.FromSeconds(10));
        using var secondResponse = await secondResponseTask.WaitAsync(TimeSpan.FromSeconds(10));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstJson = await firstResponse.Content.ReadAsStringAsync();
        firstJson.Should().Contain("Original concurrent showcase");
        firstJson.Should().NotContain("Changed between concurrent reads");
        (await secondResponse.Content.ReadAsStringAsync()).Should().Be(firstJson);
        interceptor.ReadCount.Should().Be(1, "only the first cold caller should query the daily showcase");
    }

    private WebApplicationFactory<Program> CreateAnonymousFactory(
        DateTimeOffset now,
        IMemoryCache? cache = null,
        DbCommandInterceptor? interceptor = null) =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            if (cache is not null)
            {
                services.RemoveAll<IMemoryCache>();
                services.AddSingleton(cache);
            }
            if (interceptor is not null)
                services.AddDbContext<PostgreSQLContext>(options => options.AddInterceptors(interceptor));
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, NoResultAuthHandler>(NoResultAuthHandler.TestScheme, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = NoResultAuthHandler.TestScheme;
                options.DefaultChallengeScheme = NoResultAuthHandler.TestScheme;
            });
        }));

    private sealed class ShowcaseReadInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _firstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;

        public Task FirstRead => _firstRead.Task;
        public int ReadCount => Volatile.Read(ref _readCount);
        public void ReleaseFirstRead() => _release.TrySetResult();

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("media_covers", StringComparison.Ordinal)
                && command.CommandText.Contains("expires_at", StringComparison.Ordinal)
                && Interlocked.Increment(ref _readCount) == 1)
            {
                _firstRead.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class ObservingMemoryCache(Task firstRead) : IMemoryCache
    {
        private readonly MemoryCache _inner = new(new MemoryCacheOptions());
        private readonly TaskCompletionSource _concurrentMiss = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ConcurrentMiss => _concurrentMiss.Task;

        public bool TryGetValue(object key, out object? value)
        {
            var found = _inner.TryGetValue(key, out value);
            if (!found && firstRead.IsCompleted && key is string text && text.StartsWith("media-showcase:", StringComparison.Ordinal))
                _concurrentMiss.TrySetResult();
            return found;
        }

        public ICacheEntry CreateEntry(object key) => _inner.CreateEntry(key);
        public void Remove(object key) => _inner.Remove(key);
        public void Dispose() => _inner.Dispose();
    }

    private static DateTimeOffset AtTodayUtc(int hour) =>
        new(DateTimeOffset.UtcNow.UtcDateTime.Date.AddHours(hour), TimeSpan.Zero);

    private sealed class NoResultAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string TestScheme = "NoResult";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static MediaCover CreateReadyCover(string lookupId, string imagePath, DateTimeOffset expiresAt) => new()
    {
        Provider = ArtworkProvider.Tmdb,
        LookupKind = CoverLookupKind.MovieImdb,
        LookupId = lookupId,
        Outcome = CoverOutcome.Ready,
        ProviderItemId = lookupId,
        ImagePath = imagePath,
        CheckedAt = DateTimeOffset.UtcNow,
        ExpiresAt = expiresAt
    };
}
