using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Templates.Data.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class ArtworkAuthorizationIntegrationTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task AnonymousMediaAndReviewRequests_AreUnauthorizedWithoutCreatingArtwork()
    {
        using var anonymousFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, NoResultAuthHandler>(NoResultAuthHandler.TestScheme, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = NoResultAuthHandler.TestScheme;
                options.DefaultChallengeScheme = NoResultAuthHandler.TestScheme;
            });
        }));
        using var anonymousClient = anonymousFactory.CreateClient();

        var mediaResponse = await anonymousClient.GetAsync("/api/media?mediaTypeId=-3");
        var reviewsResponse = await anonymousClient.GetAsync("/api/reviews/byMediaType/-3");

        mediaResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        reviewsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var scope = anonymousFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        (await db.MediaCovers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReviewList_DoesNotExposeAnotherUsersImportedMediaOrRequestItsArtwork()
    {
        var artwork = new RecordingArtworkService();
        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IArtworkService>();
            services.AddScoped<IArtworkService>(_ => artwork);
        }));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var template = new Template
            {
                UserId = "other-user",
                Name = "Other user movie template",
                MediaTypeId = -3,
                Fields = [new TemplateField { Name = "Story", Position = 1 }]
            };
            var movie = new MediaEntity
            {
                Title = "Other user's imported movie",
                MediaTypeId = -3,
                ExternalSource = MediaExternalSource.Imdb,
                ExternalId = "tt0133093"
            };
            db.AddRange(template, movie);
            await db.SaveChangesAsync();
            db.Reviews.Add(new Review
            {
                UserId = "other-user",
                TemplateId = template.Id,
                MediaId = movie.Id,
                OverallScore = 5,
                Fields = [new ReviewField { TemplateFieldId = template.Fields.Single().Id, Value = 5 }]
            });
            await db.SaveChangesAsync();

            using var client = factory.CreateClient();
            var response = await client.GetAsync("/api/reviews/byMediaType/-3");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var reviews = await response.Content.ReadFromJsonAsync<List<ReviewDto>>();
            reviews.Should().NotBeNull();
            reviews!.Should().NotContain(review => review.MediaId == movie.Id);
            artwork.MediaRequests.Should().NotContain(batch => batch.Contains(movie.Id));
        }
    }

    private sealed class NoResultAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string TestScheme = "NoResult";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }

    private sealed class RecordingArtworkService : IArtworkService
    {
        public List<IReadOnlyList<long>> MediaRequests { get; } = [];

        public Task<IReadOnlyDictionary<long, CoverPresentation>> GetMediaArtworkAsync(IEnumerable<long> mediaIds, CancellationToken ct = default)
        {
            MediaRequests.Add(mediaIds.ToArray());
            return Task.FromResult<IReadOnlyDictionary<long, CoverPresentation>>(new Dictionary<long, CoverPresentation>());
        }

        public Task<IReadOnlyDictionary<long, CoverPresentation>> GetCollectionArtworkAsync(IEnumerable<long> collectionIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<long, CoverPresentation>>(new Dictionary<long, CoverPresentation>());
    }
}
