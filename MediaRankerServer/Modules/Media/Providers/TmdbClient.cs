using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Providers;

public sealed record TmdbImageConfiguration(string SecureBaseUrl, IReadOnlyList<string> PosterSizes);

public sealed class TmdbClient(HttpClient httpClient, IOptions<TmdbOptions> options, TmdbRequestCooldown cooldown) : ITmdbClient
{
    private readonly TmdbOptions config = options.Value;
    private readonly SemaphoreSlim imageConfigurationGate = new(1, 1);
    private TmdbImageConfiguration? imageConfiguration;

    public async Task<ArtworkResult> GetCoverAsync(string imdbId, bool series, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(imdbId) || !imdbId.StartsWith("tt", StringComparison.Ordinal) || imdbId.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ProviderRequestException("invalid_lookup_id");
        EnsureUsable();
        cooldown.ThrowIfBlocked();

        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, $"find/{Uri.EscapeDataString(imdbId)}?external_source=imdb_id"), ct);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var resultName = series ? "tv_results" : "movie_results";
        if (!document.RootElement.TryGetProperty(resultName, out var results) || results.ValueKind != JsonValueKind.Array)
            throw new ProviderRequestException("invalid_provider_response");

        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number)
                continue;

            var path = result.TryGetProperty("poster_path", out var poster) ? poster.GetString() : null;
            return IsValidPosterPath(path)
                ? new ArtworkResult(id.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture), path)
                : new ArtworkResult(id.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture), null);
        }

        return new ArtworkResult(null, null);
    }

    public async Task<TmdbImageConfiguration> GetImageConfigurationAsync(CancellationToken ct)
    {
        if (imageConfiguration is not null)
            return imageConfiguration;

        await imageConfigurationGate.WaitAsync(ct);
        try
        {
            if (imageConfiguration is not null)
                return imageConfiguration;

            EnsureUsable();
            cooldown.ThrowIfBlocked();
            using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, "configuration"), ct);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!document.RootElement.TryGetProperty("images", out var images)
                || !images.TryGetProperty("secure_base_url", out var baseUrl)
                || !images.TryGetProperty("poster_sizes", out var sizes)
                || !Uri.TryCreate(baseUrl.GetString(), UriKind.Absolute, out var parsedBaseUrl)
                || parsedBaseUrl.Scheme != Uri.UriSchemeHttps
                || !string.Equals(parsedBaseUrl.Host, "image.tmdb.org", StringComparison.OrdinalIgnoreCase)
                || !parsedBaseUrl.AbsolutePath.StartsWith("/t/p/", StringComparison.Ordinal)
                || sizes.ValueKind != JsonValueKind.Array)
            {
                throw new ProviderRequestException("invalid_provider_response");
            }

            var posterSizes = sizes.EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => x is not null && (x == "original" || x.All(c => char.IsLetterOrDigit(c))))
                .Cast<string>()
                .ToArray();
            if (posterSizes.Length == 0)
                throw new ProviderRequestException("invalid_provider_response");

            imageConfiguration = new TmdbImageConfiguration(parsedBaseUrl.AbsoluteUri, posterSizes);
            return imageConfiguration;
        }
        finally
        {
            imageConfigurationGate.Release();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var permit = await cooldown.AcquireAsync(ct);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ReadAccessToken);
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (response.IsSuccessStatusCode)
                return response;

            var error = ToProviderException(response);
            response.Dispose();
            if (error.Code is "rate_limited" or "authentication_failed")
                cooldown.Defer(error.RetryAfter);
            throw error;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            request.Dispose();
            throw;
        }
        catch (ProviderRequestException)
        {
            request.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            request.Dispose();
            throw new ProviderRequestException("network_error", innerException: ex);
        }
    }

    private void EnsureUsable()
    {
        if (!config.IsUsable)
            throw new ProviderRequestException(config.Enabled ? "invalid_configuration" : "provider_disabled");
    }

    private static bool IsValidPosterPath(string? value) => value is not null
        && value.Length is > 1 and <= 255
        && value[0] == '/'
        && value.Skip(1).All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    private static ProviderRequestException ToProviderException(HttpResponseMessage response)
    {
        var code = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "authentication_failed",
            HttpStatusCode.TooManyRequests => "rate_limited",
            HttpStatusCode.NotFound => "not_found",
            _ when (int)response.StatusCode >= 500 => "provider_unavailable",
            _ => "provider_error"
        };
        return new ProviderRequestException(code, response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } retryDate ? retryDate - DateTimeOffset.UtcNow : null));
    }
}
