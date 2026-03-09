using System.Security.Claims;
using FluentAssertions;
using MediaRankerServer.Shared.Extensions;

namespace MediaRankerServer.UnitTests.Shared.Extensions;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetAuthenticatedUserId_WhenNameIdentifierExists_ReturnsNameIdentifierValue()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "name-id-user"));

        var userId = principal.GetAuthenticatedUserId();

        userId.Should().Be("name-id-user");
    }

    [Fact]
    public void GetAuthenticatedUserId_WhenNameIdentifierMissing_ReturnsSubClaimValue()
    {
        var principal = CreatePrincipal(new Claim("sub", "sub-user"));

        var userId = principal.GetAuthenticatedUserId();

        userId.Should().Be("sub-user");
    }

    [Fact]
    public void GetAuthenticatedUserId_WhenPrincipalIsNull_ThrowsInvalidOperationException()
    {
        ClaimsPrincipal principal = null!;

        var act = () => principal.GetAuthenticatedUserId();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Claims principal is not available.");
    }

    [Fact]
    public void GetAuthenticatedUserId_WhenNoValidClaimsExist_ThrowsInvalidOperationException()
    {
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, "   "),
            new Claim("sub", "")
        );

        var act = () => principal.GetAuthenticatedUserId();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Authenticated user id is missing from token claims.");
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test-auth"));
    }
}
