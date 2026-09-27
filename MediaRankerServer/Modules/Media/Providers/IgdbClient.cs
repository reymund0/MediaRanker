using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Providers;

public sealed class IgdbClient(
    HttpClient apiClient,
    IHttpClientFactory httpClientFactory,
    IgdbRequestLimiter limiter,
    IOptions<IgdbOptions> options) : IIgdbClient
{
    private static readonly TimeSpan AuthenticationCooldown = TimeSpan.FromMinutes(5);
    private readonly IgdbOptions config = options.Value;
    private readonly SemaphoreSlim tokenGate = new(1, 1);
    private string? accessToken;
    private DateTimeOffset accessTokenExpiresAt;

    public Task<ArtworkResult> GetCoverAsync(string gameId, CancellationToken ct) => GetCoverAsync(gameId, null, ct);

    public async Task<ArtworkResult> GetCoverAsync(string gameId, ImportWorkUnitBudget? budget, CancellationToken ct)
    {
        if (!long.TryParse(gameId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 1)
            throw new ProviderRequestException("invalid_lookup_id");

        var games = await SendGamesRequestAsync($"fields id,cover.image_id; where id = {id}; limit 1;", budget, "artwork", ct);
        var game = games.FirstOrDefault();
        return game is null || string.IsNullOrWhiteSpace(game.CoverImageId)
            ? new ArtworkResult(null, null)
            : new ArtworkResult(game.Id.ToString(CultureInfo.InvariantCulture), game.CoverImageId);
    }

    public Task<long> GetMaximumGameIdAsync(CancellationToken ct) => GetMaximumGameIdAsync(null, ct);

    public async Task<long> GetMaximumGameIdAsync(ImportWorkUnitBudget? budget, CancellationToken ct)
    {
        var games = await SendGamesRequestAsync("fields id; sort id desc; limit 1;", budget, "maximum_id", ct);
        return games.FirstOrDefault()?.Id ?? 0;
    }

    public Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(CancellationToken ct) => GetGameTypesAsync(null, ct);

    public async Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(ImportWorkUnitBudget? budget, CancellationToken ct)
    {
        const int pageSize = 500;
        var gameTypes = new List<IgdbGameType>();
        // Game types include ID zero (main games), unlike game-record IDs.
        long lastId = -1;
        while (true)
        {
            using var response = await SendApiAsync(() => CreatePostRequest("game_types", $"fields id,type; where id > {lastId}; sort id asc; limit {pageSize};"), budget, "game_types", ct);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var page = document.RootElement.EnumerateArray()
                .Where(x => x.TryGetProperty("id", out _) && x.TryGetProperty("type", out _))
                .Select(x => new IgdbGameType(x.GetProperty("id").GetInt64(), x.GetProperty("type").GetString() ?? string.Empty))
                .ToArray();
            foreach (var gameType in page)
            {
                if (gameType.Id <= lastId)
                    throw new ProviderRequestException("non_increasing_game_type_page");
                lastId = gameType.Id;
            }
            gameTypes.AddRange(page);
            if (page.Length < pageSize) return gameTypes;
        }
    }

    public Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, CancellationToken ct) => GetGamesAsync(query, null, ct);

    public async Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, ImportWorkUnitBudget? budget, CancellationToken ct)
    {
        if (query.AfterId < 0 || query.Limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(query));

        var filters = new List<string> { $"id > {query.AfterId}" };
        if (query.MaximumId is { } maximumId) filters.Add($"id <= {maximumId}");
        if (query.UpdatedAfter is { } updatedAfter) filters.Add($"updated_at > {updatedAfter.ToUnixTimeSeconds()}");
        if (query.UpdatedBefore is { } updatedBefore) filters.Add($"updated_at <= {updatedBefore.ToUnixTimeSeconds()}");

        var body = $"fields id,name,first_release_date,game_type,version_parent,cover.image_id,updated_at; where {string.Join(" & ", filters)}; sort id asc; limit {query.Limit};";
        return await SendGamesRequestAsync(body, budget, "games", ct);
    }

    private async Task<IReadOnlyList<IgdbGame>> SendGamesRequestAsync(string body, ImportWorkUnitBudget? budget,
        string operation, CancellationToken ct)
    {
        using var response = await SendApiAsync(() => CreatePostRequest("games", body), budget, operation, ct);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return document.RootElement.EnumerateArray().Select(ParseGame).ToArray();
    }

    private async Task<HttpResponseMessage> SendApiAsync(Func<HttpRequestMessage> requestFactory,
        ImportWorkUnitBudget? budget, string operation, CancellationToken ct)
    {
        EnsureUsable();
        limiter.ThrowIfBlocked();
        var token = await GetAccessTokenAsync(forceRefresh: false, rejectedToken: null, budget, ct);
        var response = await SendAuthorizedAsync(requestFactory, token, budget, operation, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        token = await GetAccessTokenAsync(forceRefresh: true, rejectedToken: token, budget, ct);
        response = await SendAuthorizedAsync(requestFactory, token, budget, operation + ".unauthorized_retry", ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        var error = new ProviderRequestException("authentication_failed", AuthenticationCooldown);
        await limiter.DeferAsync(error.RetryAfter, error.Code, ct);
        throw error;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<HttpRequestMessage> requestFactory, string token,
        ImportWorkUnitBudget? budget, string operation, CancellationToken ct)
    {
        using var lease = await limiter.AcquireAsync(ct);
        using var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Client-ID", config.ClientId);

        try
        {
            if (budget is not null)
            {
                var isGamePage = operation == "games";
                if (isGamePage && !budget.TryReservePage(out var pageReason))
                    throw new ImportBudgetExceededException(pageReason);
                if (!budget.TryReserveHttp(operation, out var httpReason))
                {
                    if (isGamePage) budget.RollbackPage();
                    throw new ImportBudgetExceededException(httpReason);
                }
            }

            var response = await apiClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                return response;

            var error = ToProviderException(response);
            response.Dispose();
            if (error.Code is "rate_limited" or "authentication_failed")
                await limiter.DeferAsync(error.RetryAfter, error.Code, CancellationToken.None);
            throw error;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ProviderRequestException)
        {
            throw;
        }
        catch (ImportBudgetExceededException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProviderRequestException("network_error", innerException: ex);
        }
    }

    private async Task<string> GetAccessTokenAsync(bool forceRefresh, string? rejectedToken,
        ImportWorkUnitBudget? budget, CancellationToken ct)
    {
        limiter.ThrowIfBlocked();
        if (!forceRefresh && accessToken is not null && accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return accessToken;
        if (forceRefresh && accessToken is not null && accessToken != rejectedToken && accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return accessToken;

        await tokenGate.WaitAsync(ct);
        try
        {
            limiter.ThrowIfBlocked();
            if (!forceRefresh && accessToken is not null && accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return accessToken;
            if (forceRefresh && accessToken is not null && accessToken != rejectedToken && accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return accessToken;

            using var lease = await limiter.AcquireAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, "oauth2/token")
            {
                Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("client_id", config.ClientId!),
                    new KeyValuePair<string, string>("client_secret", config.ClientSecret!),
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                ])
            };

            if (budget is not null)
                budget.ThrowIfHttpUnavailable("token");

            using var response = await httpClientFactory.CreateClient(MediaProviderServiceCollectionExtensions.IgdbTwitchClientName)
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = ToProviderException(response);
                await limiter.DeferAsync(error.RetryAfter, error.Code, CancellationToken.None);
                throw error;
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var token = document.RootElement.TryGetProperty("access_token", out var tokenElement) ? tokenElement.GetString() : null;
            var seconds = document.RootElement.TryGetProperty("expires_in", out var expiryElement) && expiryElement.TryGetInt32(out var parsed) ? parsed : 0;
            if (string.IsNullOrWhiteSpace(token) || seconds <= 0)
            {
                var error = new ProviderRequestException("invalid_auth_response");
                await limiter.DeferAsync(error.RetryAfter, error.Code, CancellationToken.None);
                throw error;
            }

            accessToken = token;
            accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(seconds);
            return accessToken;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ProviderRequestException)
        {
            throw;
        }
        catch (ImportBudgetExceededException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var error = new ProviderRequestException("authentication_error", innerException: ex);
            await limiter.DeferAsync(error.RetryAfter, error.Code, CancellationToken.None);
            throw error;
        }
        finally
        {
            tokenGate.Release();
        }
    }

    private HttpRequestMessage CreatePostRequest(string path, string body) => new(HttpMethod.Post, path)
    {
        Content = new StringContent(body, Encoding.UTF8, "text/plain")
    };

    private void EnsureUsable()
    {
        if (!config.IsUsable)
            throw new ProviderRequestException(config.IsEnabled ? "invalid_configuration" : "provider_disabled");
    }

    private static IgdbGame ParseGame(JsonElement element)
    {
        var id = element.GetProperty("id").GetInt64();
        var release = ReadUnixTime(element, "first_release_date");
        var updated = ReadUnixTime(element, "updated_at");
        long? gameType = element.TryGetProperty("game_type", out var gameTypeElement) && gameTypeElement.ValueKind == JsonValueKind.Number
            ? gameTypeElement.GetInt64() : null;
        long? versionParent = element.TryGetProperty("version_parent", out var versionParentElement) && versionParentElement.ValueKind == JsonValueKind.Number
            ? versionParentElement.GetInt64() : null;
        var cover = element.TryGetProperty("cover", out var coverElement) && coverElement.ValueKind == JsonValueKind.Object
            && coverElement.TryGetProperty("image_id", out var imageIdElement) ? imageIdElement.GetString() : null;
        return new IgdbGame(id, element.TryGetProperty("name", out var name) ? name.GetString() : null, release, gameType, versionParent, cover, updated);
    }

    private static DateTimeOffset? ReadUnixTime(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;

    private static ProviderRequestException ToProviderException(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } retryDate ? retryDate - DateTimeOffset.UtcNow : null);
        var code = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "authentication_failed",
            HttpStatusCode.TooManyRequests => "rate_limited",
            HttpStatusCode.NotFound => "not_found",
            _ when (int)response.StatusCode >= 500 => "provider_unavailable",
            _ => "provider_error"
        };
        if (code == "authentication_failed" && retryAfter.GetValueOrDefault() <= TimeSpan.Zero)
            retryAfter = AuthenticationCooldown;
        return new ProviderRequestException(code, retryAfter);
    }
}
