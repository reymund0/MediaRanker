using System.Net;
using System.Text;
using FluentAssertions;
using MediaRankerServer.Modules.Media.Providers;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.UnitTests.Modules.Media.Providers;

public class IgdbClientTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1440, true)]
    [InlineData(1441, false)]
    public void IncrementalOverlapMustReplayTimestampBoundaries(int minutes, bool expected)
    {
        new IgdbOptions { IncrementalOverlapMinutes = minutes }.HasValidLimits.Should().Be(expected);
    }

    [Fact]
    public async Task TokenNearExpiryIsRenewed_AndCatalogAndArtworkShareIt()
    {
        var tokenCalls = 0;
        var api = new DelegateHandler(_ => Json("""[{"id":42}]"""));
        var twitch = new DelegateHandler(_ => Json(Interlocked.Increment(ref tokenCalls) == 1
            ? """{"access_token":"short-lived","expires_in":30}"""
            : """{"access_token":"renewed","expires_in":3600}"""));
        var client = Create(api, twitch);
        await client.GetMaximumGameIdAsync(CancellationToken.None);
        await client.GetCoverAsync("42", CancellationToken.None);
        await client.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), CancellationToken.None);
        twitch.Requests.Should().HaveCount(2);
        api.Requests.Skip(1).Should().OnlyContain(x => x.Headers.Authorization!.Parameter == "renewed");
    }

    [Fact]
    public async Task CanceledCallDoesNotStartHttp_AndSharedCooldownFailsFast()
    {
        var api = new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var twitch = new DelegateHandler(_ => Json("""{"access_token":"token","expires_in":3600}"""));
        var client = Create(api, twitch);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await client.Invoking(x => x.GetCoverAsync("42", canceled.Token)).Should().ThrowAsync<OperationCanceledException>();
        api.Requests.Should().BeEmpty();
        twitch.Requests.Should().BeEmpty();
        await client.Invoking(x => x.GetCoverAsync("42", CancellationToken.None)).Should().ThrowAsync<ProviderRequestException>();
        var blockedCall = client.GetMaximumGameIdAsync(CancellationToken.None);
        await blockedCall.Invoking(x => x.WaitAsync(TimeSpan.FromSeconds(1))).Should().ThrowAsync<ProviderRequestException>();
        api.Requests.Should().HaveCount(1);
        twitch.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCoverAsync_UsesCachedTwitchTokenAndReturnsCoverAsset()
    {
        var api = new DelegateHandler(_ => Json("""[{"id":42,"cover":{"image_id":"co1abc"}}]"""));
        var twitch = new DelegateHandler(_ => Json("""{"access_token":"token-one","expires_in":3600}"""));
        var client = Create(api, twitch);

        var first = await client.GetCoverAsync("42", CancellationToken.None);
        var second = await client.GetCoverAsync("42", CancellationToken.None);

        first.Should().Be(new ArtworkResult("42", "co1abc"));
        second.Should().Be(first);
        twitch.Requests.Should().HaveCount(1);
        api.Requests.Should().HaveCount(2);
        api.Requests.Should().OnlyContain(x => x.Headers.Authorization!.Parameter == "token-one" && x.Headers.Contains("Client-ID"));
    }

    [Fact]
    public async Task GetCoverAsync_RepeatedUnauthorized_ProducesSanitizedCooldownFailure()
    {
        var api = new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var twitch = new DelegateHandler(_ => Json("""{"access_token":"token","expires_in":3600}"""));
        var client = Create(api, twitch);

        var act = () => client.GetCoverAsync("42", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<ProviderRequestException>();
        failure.Which.Code.Should().Be("authentication_failed");
        failure.Which.RetryAfter.Should().Be(TimeSpan.FromMinutes(5));
        twitch.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ForbiddenResponse_ActivatesSharedCooldownAcrossCatalogAndArtworkCalls()
    {
        var api = new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var twitch = new DelegateHandler(_ => Json("""{"access_token":"token","expires_in":3600}"""));
        var client = Create(api, twitch);

        var firstFailure = await client.Invoking(x => x.GetCoverAsync("42", CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();

        firstFailure.Which.Code.Should().Be("authentication_failed");
        firstFailure.Which.RetryAfter.Should().Be(TimeSpan.FromMinutes(5));
        await client.Invoking(x => x.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();
        await client.Invoking(x => x.GetCoverAsync("43", CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();

        api.Requests.Should().ContainSingle();
        twitch.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetCoverAsync_ConcurrentUnauthorizedResponsesShareOneReplacementToken()
    {
        var tokenRequests = 0;
        var api = new DelegateHandler(request => request.Headers.Authorization!.Parameter == "expired"
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : Json("""[{"id":42,"cover":{"image_id":"co1abc"}}]"""));
        var twitch = new DelegateHandler(_ => Json(Interlocked.Increment(ref tokenRequests) == 1
            ? """{"access_token":"expired","expires_in":3600}"""
            : """{"access_token":"fresh","expires_in":3600}"""));
        var client = Create(api, twitch);

        var results = await Task.WhenAll(
            client.GetCoverAsync("42", CancellationToken.None),
            client.GetCoverAsync("42", CancellationToken.None));

        results.Should().OnlyContain(x => x == new ArtworkResult("42", "co1abc"));
        twitch.Requests.Should().HaveCount(2, "one initial token and one serialized replacement are sufficient");
    }

    [Fact]
    public async Task GetCoverAsync_RateLimit_ExposesRetryAfterWithoutProviderPayload()
    {
        var api = new DelegateHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
            return response;
        });
        var twitch = new DelegateHandler(_ => Json("""{"access_token":"token","expires_in":3600}"""));
        var client = Create(api, twitch);

        var act = () => client.GetCoverAsync("42", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<ProviderRequestException>();
        failure.Which.Code.Should().Be("rate_limited");
        failure.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(2));
        failure.Which.Message.Should().NotContain("token");
    }

    [Fact]
    public async Task GetCoverAsync_Disabled_DoesNotRequestTokenOrApi()
    {
        var api = new DelegateHandler(_ => throw new InvalidOperationException("HTTP must not be called"));
        var twitch = new DelegateHandler(_ => throw new InvalidOperationException("HTTP must not be called"));
        var options = new IgdbOptions();
        var client = new IgdbClient(new HttpClient(api) { BaseAddress = new Uri("https://api.igdb.com/v4/") },
            new TestHttpClientFactory(new HttpClient(twitch) { BaseAddress = new Uri("https://id.twitch.tv/") }),
            new IgdbRequestLimiter(options), Options.Create(options));

        var act = () => client.GetCoverAsync("42", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<ProviderRequestException>();
        failure.Which.Code.Should().Be("provider_disabled");
        api.Requests.Should().BeEmpty();
        twitch.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGameTypesAsync_ReadsDocumentedTypeLabels()
    {
        var api = new DelegateHandler(request =>
        {
            request.RequestUri!.AbsolutePath.Should().Be("/v4/game_types");
            request.Content!.ReadAsStringAsync().Result.Should().Contain("fields id,type;");
            request.Content.ReadAsStringAsync().Result.Should().Contain("where id > -1;");
            return Json("""[{"id":0,"type":"Main Game"},{"id":8,"type":"Remake"},{"id":9,"type":"Remaster"}]""");
        });
        var twitch = new DelegateHandler(_ => Json("""{"access_token":"token","expires_in":3600}"""));

        var types = await Create(api, twitch).GetGameTypesAsync(CancellationToken.None);

        types.Should().Equal(new IgdbGameType(0, "Main Game"), new IgdbGameType(8, "Remake"), new IgdbGameType(9, "Remaster"));
    }

    [Fact]
    public async Task GetGameTypesAsync_PaginatesBeyondTheDocumentedPageLimit()
    {
        var requestCount = 0;
        var api = new DelegateHandler(request =>
        {
            request.Content!.ReadAsStringAsync().Result.Should().Contain("limit 500");
            var page = Interlocked.Increment(ref requestCount) == 1
                ? "[" + string.Join(',', Enumerable.Range(1, 500).Select(id => $"{{\"id\":{id},\"type\":\"Type {id}\"}}")) + "]"
                : "[{\"id\":501,\"type\":\"Final\"}]";
            return Json(page);
        });
        var twitch = new DelegateHandler(_ => Json("""{"access_token":"token","expires_in":3600}"""));
        var client = Create(api, twitch);

        var types = await client.GetGameTypesAsync(CancellationToken.None);

        types.Should().HaveCount(501);
        types[^1].Should().Be(new IgdbGameType(501, "Final"));
        requestCount.Should().Be(2);
    }

    private static IgdbClient Create(DelegateHandler api, DelegateHandler twitch)
    {
        var options = new IgdbOptions { ImportEnabled = true, ClientId = "client-id", ClientSecret = "client-secret", RequestsPerSecond = 4 };
        return new IgdbClient(new HttpClient(api) { BaseAddress = new Uri("https://api.igdb.com/v4/") },
            new TestHttpClientFactory(new HttpClient(twitch) { BaseAddress = new Uri("https://id.twitch.tv/") }),
            new IgdbRequestLimiter(options), Options.Create(options));
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class TestHttpClientFactory(HttpClient twitch) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => twitch;
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
