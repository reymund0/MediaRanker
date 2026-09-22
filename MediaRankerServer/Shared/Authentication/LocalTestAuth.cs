using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MediaRankerServer.Shared.Authentication;

public enum LocalTestAuthDecision
{
    NotRequested,
    Disabled,
    NotDevelopment,
    NonLoopback,
    Accepted
}

public static class LocalTestAuth
{
    public const string Scheme = "LocalTest";
    public const string PolicyScheme = "CognitoOrLocalTest";
    public const string BearerToken = "MediaRankerLocalTest";
    public const string UserId = "local-test-user";

    public static LocalTestAuthDecision Evaluate(HttpContext context, LocalTestAuthOptions options, IHostEnvironment environment)
    {
        if (!HasSentinelBearer(context.Request)) return LocalTestAuthDecision.NotRequested;
        if (!options.Enabled) return LocalTestAuthDecision.Disabled;
        if (!environment.IsDevelopment()) return LocalTestAuthDecision.NotDevelopment;
        return IsDirectLoopback(context) ? LocalTestAuthDecision.Accepted : LocalTestAuthDecision.NonLoopback;
    }

    public static bool ShouldUseLocalHandler(HttpContext context, LocalTestAuthOptions options, IHostEnvironment environment) =>
        HasSentinelBearer(context.Request) && options.Enabled && environment.IsDevelopment();

    public static ClaimsPrincipal CreatePrincipal()
    {
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim("sub", UserId),
            new Claim(ClaimTypes.Name, "Local test user")
        ], Scheme);
        return new ClaimsPrincipal(identity);
    }

    private static bool HasSentinelBearer(HttpRequest request) =>
        request.Headers.Authorization.Count == 1
        && string.Equals(request.Headers.Authorization[0], $"Bearer {BearerToken}", StringComparison.Ordinal);

    private static bool IsDirectLoopback(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null || !IPAddress.IsLoopback(remoteIp)) return false;

        var host = context.Request.Host.Host;
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var hostIp) && IPAddress.IsLoopback(hostIp));
    }
}
