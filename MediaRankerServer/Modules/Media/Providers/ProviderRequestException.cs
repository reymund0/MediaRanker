namespace MediaRankerServer.Modules.Media.Providers;

public sealed class ProviderRequestException(string code, TimeSpan? retryAfter = null, Exception? innerException = null,
    bool isLocalCooldown = false)
    : Exception($"Provider request failed: {code}", innerException)
{
    public string Code { get; } = code;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public bool IsLocalCooldown { get; } = isLocalCooldown;
}
