using System.Net;
using System.Text;
using FluentAssertions;
using MediaRankerServer.Modules.Media.Providers;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.UnitTests.Modules.Media.Providers;

public class TmdbClientTests
{
    [Fact]
    public async Task SharedBudgetSerializesCallsAcrossClientInstances_AndCancelsWaitingCall()
    {
        var limiter = new TmdbRequestCooldown(new TmdbOptions { RequestsPerSecond = 20, MaxConcurrentRequests = 1 });
        using var first = await limiter.AcquireAsync(CancellationToken.None);
        using var canceled = new CancellationTokenSource();
        var waiting = limiter.AcquireAsync(canceled.Token);
        waiting.IsCompleted.Should().BeFalse();
        canceled.Cancel();
        await waiting.Invoking(x => x).Should().ThrowAsync<OperationCanceledException>();
        first.Dispose();
        using var next = await limiter.AcquireAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(21, 1)]
    [InlineData(4, 0)]
    [InlineData(4, 9)]
    public void OptionsRejectInvalidSharedBudgets(int requests, int concurrency)
    {
        new TmdbOptions { RequestsPerSecond = requests, MaxConcurrentRequests = concurrency }.HasValidLimits.Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "authentication_failed")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "provider_unavailable")]
    public async Task GetCoverAsync_HttpFailureIsNotANegativeCacheResult(HttpStatusCode status, string code)
    {
        var client = Create(new DelegateHandler(_ => new HttpResponseMessage(status)));
        var failure = await client.Invoking(x => x.GetCoverAsync("tt0123456", false, CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();
        failure.Which.Code.Should().Be(code);
    }

    [Fact]
    public async Task GetCoverAsync_TimeoutIsTransient_AndCallerCancellationPropagates()
    {
        var client = Create(new DelegateHandler(_ => throw new TaskCanceledException("simulated timeout")));
        var failure = await client.Invoking(x => x.GetCoverAsync("tt0123456", false, CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();
        failure.Which.Code.Should().Be("network_error");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await client.Invoking(x => x.GetCoverAsync("tt0123456", false, canceled.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetCoverAsync_WrongResultKindCannotSupplyMovieArtwork()
    {
        var client = Create(new DelegateHandler(_ => Json("""{"movie_results":[],"tv_results":[{"id":2,"poster_path":"/series.jpg"}]}""")));
        (await client.GetCoverAsync("tt0123456", false, CancellationToken.None)).Should().Be(new ArtworkResult(null, null));
    }

    [Fact]
    public async Task GetCoverAsync_MovieResultWithPoster_ReturnsProviderAssetOnly()
    {
        var handler = new DelegateHandler(_ => Json("""{"movie_results":[{"id":55,"poster_path":"/poster_1.jpg"}],"tv_results":[]}"""));
        var client = Create(handler);

        var result = await client.GetCoverAsync("tt0123456", series: false, CancellationToken.None);

        result.Should().Be(new ArtworkResult("55", "/poster_1.jpg"));
        handler.Requests.Single().Headers.Authorization!.Scheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task GetCoverAsync_SeriesIgnoresMovieResults_AndMissingPosterIsNotReady()
    {
        var handler = new DelegateHandler(_ => Json("""{"movie_results":[{"id":1,"poster_path":"/movie.jpg"}],"tv_results":[{"id":2,"poster_path":null}]}"""));
        var client = Create(handler);

        var result = await client.GetCoverAsync("tt0123456", series: true, CancellationToken.None);

        result.Should().Be(new ArtworkResult("2", null));
    }

    [Fact]
    public async Task GetCoverAsync_Disabled_DoesNotSendHttp()
    {
        var handler = new DelegateHandler(_ => throw new InvalidOperationException("HTTP must not be called"));
        var client = new TmdbClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") },
            Options.Create(new TmdbOptions()), new TmdbRequestCooldown());

        var act = () => client.GetCoverAsync("tt0123456", false, CancellationToken.None);

        var failure = await act.Should().ThrowAsync<ProviderRequestException>();
        failure.Which.Code.Should().Be("provider_disabled");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetImageConfigurationAsync_RejectsNonHttpsCdn()
    {
        var handler = new DelegateHandler(_ => Json("""{"images":{"secure_base_url":"http://example.test/","poster_sizes":["w342"]}}"""));
        var client = Create(handler);

        var act = () => client.GetImageConfigurationAsync(CancellationToken.None);

        var failure = await act.Should().ThrowAsync<ProviderRequestException>();
        failure.Which.Code.Should().Be("invalid_provider_response");
    }

    [Fact]
    public async Task GetCoverAsync_RateLimitPausesAllTypedClients()
    {
        var cooldown = new TmdbRequestCooldown();
        var throttledHandler = new DelegateHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
            return response;
        });
        var blockedHandler = new DelegateHandler(_ => throw new InvalidOperationException("shared cooldown should prevent this request"));
        var first = Create(throttledHandler, cooldown);
        var second = Create(blockedHandler, cooldown);

        await first.Invoking(x => x.GetCoverAsync("tt0123456", false, CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();
        var blocked = await second.Invoking(x => x.GetCoverAsync("tt0123456", false, CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();

        blocked.Which.Code.Should().Be("rate_limited");
        blockedHandler.Requests.Should().BeEmpty();
    }

    private static TmdbClient Create(DelegateHandler handler, TmdbRequestCooldown? cooldown = null) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") },
        Options.Create(new TmdbOptions { Enabled = true, ReadAccessToken = "test-token" }), cooldown ?? new TmdbRequestCooldown());

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

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
