using System.Security.Claims;

namespace MediaRankerServer.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetAuthenticatedUserId(this ClaimsPrincipal principal)
    {
        if (principal is null)
        {
            throw new InvalidOperationException("Claims principal is not available.");
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Authenticated user id is missing from token claims.");
        }

        return userId;
    }
}
