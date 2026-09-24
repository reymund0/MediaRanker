using System.Collections.Concurrent;
using System.Net;
using System.Text;
using FluentAssertions;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.UnitTests.Modules.Media.Providers;

public sealed class IgdbClientBudgetTests
{
    [Fact]
    public async Task ColdTokenAndPageChargeActualSendsByOperation()
    {
        var api = new RecordingHandler(_ => Json("[{\"id\":42}]"));
        var twitch = new RecordingHandler(_ => Json("{\"access_token\":\"token\",\"expires_in\":3600}"));
        var client = Create(api, twitch);
        using var session = new ImportBudgetSession(new ImportSessionLimits(2, 10, TimeSpan.FromMinutes(1)));
        var unit = session.CreateUnit(new ImportWorkUnitLimits(5, 2, 10, 5, TimeSpan.FromMinutes(1)));

        await client.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), unit, CancellationToken.None);

        session.HttpAttempts.Should().Be(2);
        session.HttpAttemptsByOperation.Should().Contain(new KeyValuePair<string, int>("token", 1));
        session.HttpAttemptsByOperation.Should().Contain(new KeyValuePair<string, int>("games", 1));
        twitch.Requests.Should().ContainSingle();
        api.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task SessionCapStopsBeforeAnUnsentRetry()
    {
        var api = new RecordingHandler(_ => Json("[{\"id\":42}]"));
        var twitch = new RecordingHandler(_ => Json("{\"access_token\":\"token\",\"expires_in\":3600}"));
        var client = Create(api, twitch);
        using var session = new ImportBudgetSession(new ImportSessionLimits(1, 10, TimeSpan.FromMinutes(1)));
        var unit = session.CreateUnit(new ImportWorkUnitLimits(5, 5, 10, 5, TimeSpan.FromMinutes(1)));

        var failure = await client.Invoking(x => x.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), unit, CancellationToken.None))
            .Should().ThrowAsync<ImportBudgetExceededException>();
        failure.Which.Reason.Should().Be(ImportStopReason.SessionHttpLimit);

        twitch.Requests.Should().ContainSingle();
        api.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task UnauthorizedResendChargesFreshTokenAndApiAttempts()
    {
        var api = new RecordingHandler(request => request.Headers.Authorization?.Parameter == "old"
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : Json("[{\"id\":42}]"));
        var twitchCalls = 0;
        var twitch = new RecordingHandler(_ => Interlocked.Increment(ref twitchCalls) == 1
            ? Json("{\"access_token\":\"old\",\"expires_in\":3600}")
            : Json("{\"access_token\":\"new\",\"expires_in\":3600}"));
        var client = Create(api, twitch);
        using var session = new ImportBudgetSession(new ImportSessionLimits(4, 10, TimeSpan.FromMinutes(1)));
        var unit = session.CreateUnit(new ImportWorkUnitLimits(1, 4, 10, 5, TimeSpan.FromMinutes(1)));

        (await client.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), unit, CancellationToken.None))
            .Should().ContainSingle();

        session.HttpAttempts.Should().Be(4);
        unit.Pages.Should().Be(1, "the authorized resend belongs to the same game page");
        session.HttpAttemptsByOperation["games"].Should().Be(1);
        session.HttpAttemptsByOperation["games.unauthorized_retry"].Should().Be(1);
        session.HttpAttemptsByOperation["token"].Should().Be(2);
        api.Requests.Should().HaveCount(2);
        twitch.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task WarmTokenChargesOnlyEachCatalogSend()
    {
        var api = new RecordingHandler(_ => Json("[{\"id\":42}]"));
        var twitch = new RecordingHandler(_ => Json("{\"access_token\":\"token\",\"expires_in\":3600}"));
        var client = Create(api, twitch);
        using var session = new ImportBudgetSession(new ImportSessionLimits(3, 10, TimeSpan.FromMinutes(1)));
        var unit = session.CreateUnit(new ImportWorkUnitLimits(5, 3, 10, 5, TimeSpan.FromMinutes(1)));

        await client.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), unit, CancellationToken.None);
        await client.GetGamesAsync(new IgdbGameQuery(42, 100, null, null, 10), unit, CancellationToken.None);

        session.HttpAttempts.Should().Be(3);
        session.HttpAttemptsByOperation["token"].Should().Be(1);
        session.HttpAttemptsByOperation["games"].Should().Be(2);
        twitch.Requests.Should().ContainSingle();
        api.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GameTypePaginationChargesTerminalEmptyPage()
    {
        var calls = 0;
        var firstPage = "[" + string.Join(',', Enumerable.Range(1, 500).Select(x => $"{{\"id\":{x},\"type\":\"Type\"}}")) + "]";
        var api = new RecordingHandler(_ => Interlocked.Increment(ref calls) == 1
            ? Json(firstPage)
            : Json("[]"));
        var twitch = new RecordingHandler(_ => Json("{\"access_token\":\"token\",\"expires_in\":3600}"));
        var client = Create(api, twitch);
        using var session = new ImportBudgetSession(new ImportSessionLimits(3, 10, TimeSpan.FromMinutes(1)));
        var unit = session.CreateUnit(new ImportWorkUnitLimits(1, 3, 10, 5, TimeSpan.FromMinutes(1)));

        var types = await client.GetGameTypesAsync(unit, CancellationToken.None);

        types.Should().HaveCount(500);
        unit.Pages.Should().Be(0, "game type discovery uses HTTP allowance, not game page allowance");
        session.HttpAttempts.Should().Be(3);
        session.HttpAttemptsByOperation["game_types"].Should().Be(2);
        session.HttpAttemptsByOperation["token"].Should().Be(1);
        api.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task RepeatedGameTypePageFailsBeforeSpendingTheRemainingBudget()
    {
        var page = "[" + string.Join(',', Enumerable.Range(0, 500).Select(x => $"{{\"id\":{x},\"type\":\"Type\"}}")) + "]";
        var api = new RecordingHandler(_ => Json(page));
        var twitch = new RecordingHandler(_ => Json("{\"access_token\":\"token\",\"expires_in\":3600}"));
        var client = Create(api, twitch);
        using var session = new ImportBudgetSession(new ImportSessionLimits(10, 10, TimeSpan.FromMinutes(1)));
        using var unit = session.CreateUnit(new ImportWorkUnitLimits(1, 10, 10, 5, TimeSpan.FromMinutes(1)));

        var failure = await client.Invoking(x => x.GetGameTypesAsync(unit, CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();

        failure.Which.Code.Should().Be("non_increasing_game_type_page");
        api.Requests.Should().HaveCount(2);
        session.HttpAttempts.Should().Be(3);
        unit.Pages.Should().Be(0);
    }

    [Fact]
    public async Task LocalCooldownRejectsWithoutChargingARequest()
    {
        var api = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(1));
            return response;
        });
        var twitch = new RecordingHandler(_ => Json("{\"access_token\":\"token\",\"expires_in\":3600}"));
        var client = Create(api, twitch);
        using var session = new ImportBudgetSession(new ImportSessionLimits(4, 10, TimeSpan.FromMinutes(1)));
        var first = session.CreateUnit(new ImportWorkUnitLimits(5, 4, 10, 5, TimeSpan.FromMinutes(1)));
        await client.Invoking(x => x.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), first, CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();

        using var secondSession = new ImportBudgetSession(new ImportSessionLimits(4, 10, TimeSpan.FromMinutes(1)));
        var second = secondSession.CreateUnit(new ImportWorkUnitLimits(5, 4, 10, 5, TimeSpan.FromMinutes(1)));
        var blocked = await client.Invoking(x => x.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), second, CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();

        blocked.Which.IsLocalCooldown.Should().BeTrue();
        secondSession.HttpAttempts.Should().Be(0);
        api.Requests.Should().ContainSingle();
        twitch.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ProviderFailuresActivateSharedCooldown(HttpStatusCode status)
    {
        var api = new RecordingHandler(_ => new HttpResponseMessage(status));
        var twitch = new RecordingHandler(_ => Json("{\"access_token\":\"token\",\"expires_in\":3600}"));
        var client = Create(api, twitch);

        await client.Invoking(x => x.GetCoverAsync("42", CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();
        await client.Invoking(x => x.GetMaximumGameIdAsync(CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();

        api.Requests.Should().ContainSingle();
        twitch.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("invalid")]
    public async Task TokenFailuresActivateSharedCooldown(string mode)
    {
        var api = new RecordingHandler(_ => Json("[{\"id\":42}]"));
        var twitch = new RecordingHandler(_ => mode switch
        {
            "network" => throw new HttpRequestException("fixture network failure"),
            "timeout" => throw new TaskCanceledException("fixture timeout"),
            _ => Json("{\"access_token\":null,\"expires_in\":0}")
        });
        var client = Create(api, twitch);

        await client.Invoking(x => x.GetCoverAsync("42", CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();
        await client.Invoking(x => x.GetCoverAsync("42", CancellationToken.None))
            .Should().ThrowAsync<ProviderRequestException>();

        twitch.Requests.Should().ContainSingle();
        api.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportTokenRefreshIsChargedOnceWhenArtworkWaitsForIt()
    {
        var tokenStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseToken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new RecordingHandler(_ => Json("[{\"id\":42}]"));
        var twitch = new RecordingHandler(async (_, _) =>
        {
            tokenStarted.SetResult();
            await releaseToken.Task;
            return Json("{\"access_token\":\"token\",\"expires_in\":3600}");
        });
        var client = Create(api, twitch);
        using var session = new ImportBudgetSession(new ImportSessionLimits(3, 10, TimeSpan.FromMinutes(1)));
        var importUnit = session.CreateUnit(new ImportWorkUnitLimits(5, 3, 10, 5, TimeSpan.FromMinutes(1)));
        var import = client.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), importUnit, CancellationToken.None);
        await tokenStarted.Task;
        var artwork = client.GetCoverAsync("42", CancellationToken.None);
        releaseToken.SetResult();
        await Task.WhenAll(import, artwork);

        session.HttpAttempts.Should().Be(2);
        session.HttpAttemptsByOperation["token"].Should().Be(1);
        session.HttpAttemptsByOperation["games"].Should().Be(1);
        twitch.Requests.Should().ContainSingle();
        api.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ArtworkInitiatedTokenRefreshIsNotChargedToWaitingImport()
    {
        var tokenStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseToken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new RecordingHandler(_ => Json("[{\"id\":42,\"cover\":{\"image_id\":\"co1abc\"}}]"));
        var twitch = new RecordingHandler(async (_, _) =>
        {
            tokenStarted.SetResult();
            await releaseToken.Task;
            return Json("{\"access_token\":\"token\",\"expires_in\":3600}");
        });
        var client = Create(api, twitch, requestsPerSecond: 1000);

        var artwork = client.GetCoverAsync("42", CancellationToken.None);
        await tokenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var session = new ImportBudgetSession(new ImportSessionLimits(1, 10, TimeSpan.FromMinutes(1)));
        using var importUnit = session.CreateUnit(new ImportWorkUnitLimits(1, 1, 10, 1, TimeSpan.FromMinutes(1)));
        var import = client.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), importUnit, CancellationToken.None);
        releaseToken.SetResult();

        var artworkResult = await artwork;
        var games = await import;

        artworkResult.Should().Be(new ArtworkResult("42", "co1abc"));
        games.Should().ContainSingle();
        session.HttpAttempts.Should().Be(1, "the artwork flow initiated the token send outside the import budget");
        session.HttpAttemptsByOperation.Should().NotContainKey("token");
        session.HttpAttemptsByOperation["games"].Should().Be(1);
        twitch.Requests.Should().ContainSingle();
        api.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExhaustedImportDoesNotPoisonIndependentArtwork()
    {
        var api = new RecordingHandler(_ => Json("[{\"id\":42,\"cover\":{\"image_id\":\"co1abc\"}}]"));
        var twitch = new RecordingHandler(_ => Json("{\"access_token\":\"token\",\"expires_in\":3600}"));
        var client = Create(api, twitch);
        using var session = new ImportBudgetSession(new ImportSessionLimits(1, 10, TimeSpan.FromMinutes(1)));
        var importUnit = session.CreateUnit(new ImportWorkUnitLimits(5, 5, 10, 5, TimeSpan.FromMinutes(1)));

        await client.Invoking(x => x.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), importUnit, CancellationToken.None))
            .Should().ThrowAsync<ImportBudgetExceededException>();
        var artwork = await client.GetCoverAsync("42", CancellationToken.None);

        artwork.Should().Be(new ArtworkResult("42", "co1abc"));
        twitch.Requests.Should().ContainSingle();
        api.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task DeadlineCancelsInFlightApiRequest()
    {
        var api = new RecordingHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Json("[]");
        });
        var twitch = new RecordingHandler(_ => Json("{\"access_token\":\"token\",\"expires_in\":3600}"));
        var client = Create(api, twitch, requestsPerSecond: 1000);
        using var session = new ImportBudgetSession(new ImportSessionLimits(3, 10, TimeSpan.FromMilliseconds(50)));
        var unit = session.CreateUnit(new ImportWorkUnitLimits(5, 3, 10, 5, TimeSpan.FromSeconds(1)));
        using var linked = unit.CreateLinkedTokenSource();

        await client.Invoking(x => x.GetGamesAsync(new IgdbGameQuery(0, 100, null, null, 10), unit, linked.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        api.Requests.Should().ContainSingle();
    }

    private static IgdbClient Create(RecordingHandler api, RecordingHandler twitch, int requestsPerSecond = 4)
    {
        var options = new IgdbOptions { ImportEnabled = true, ClientId = "client-id", ClientSecret = "client-secret", RequestsPerSecond = requestsPerSecond };
        return new IgdbClient(new HttpClient(api) { BaseAddress = new Uri("https://api.igdb.com/v4/") },
            new TestFactory(new HttpClient(twitch) { BaseAddress = new Uri("https://id.twitch.tv/") }),
            new IgdbRequestLimiter(options), Options.Create(options));
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class TestFactory(HttpClient twitch) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => twitch;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage>? responder;
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? asyncResponder;
        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => this.responder = responder;
        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) => asyncResponder = responder;
        public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            return asyncResponder is not null ? asyncResponder(request, cancellationToken) : Task.FromResult(responder!(request));
        }
    }
}
