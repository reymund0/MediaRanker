using System.Net;
using System.Security.Claims;
using FluentAssertions;
using MediaRankerServer.Shared.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MediaRankerServer.Shared.Extensions;

namespace MediaRankerServer.UnitTests.Shared.Authentication;

public class LocalTestAuthTests
{
    [Fact]
    public void Evaluate_EnabledDevelopmentLoopbackSentinel_AcceptsFixedUser()
    {
        var context = CreateContext(LocalTestAuth.BearerToken, IPAddress.Loopback, "localhost");

        var decision = LocalTestAuth.Evaluate(context, new LocalTestAuthOptions { Enabled = true }, DevelopmentEnvironment);
        var principal = LocalTestAuth.CreatePrincipal();

        decision.Should().Be(LocalTestAuthDecision.Accepted);
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(LocalTestAuth.UserId);
        principal.FindFirstValue("sub").Should().Be(LocalTestAuth.UserId);
        principal.Identity!.AuthenticationType.Should().Be(LocalTestAuth.Scheme);
    }

    [Fact]
    public void Evaluate_DisabledOrProduction_RejectsSentinel()
    {
        var context = CreateContext(LocalTestAuth.BearerToken, IPAddress.Loopback, "localhost");

        LocalTestAuth.Evaluate(context, new LocalTestAuthOptions { Enabled = false }, DevelopmentEnvironment)
            .Should().Be(LocalTestAuthDecision.Disabled);
        LocalTestAuth.Evaluate(context, new LocalTestAuthOptions { Enabled = true }, ProductionEnvironment)
            .Should().Be(LocalTestAuthDecision.NotDevelopment);
        LocalTestAuth.ShouldUseLocalHandler(context, new LocalTestAuthOptions { Enabled = false }, DevelopmentEnvironment)
            .Should().BeFalse();
        LocalTestAuth.ShouldUseLocalHandler(context, new LocalTestAuthOptions { Enabled = true }, ProductionEnvironment)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("203.0.113.10", "localhost")]
    [InlineData("127.0.0.1", "example.test")]
    public void Evaluate_NonLoopbackConnectionOrHost_RejectsSentinel(string remoteIp, string host)
    {
        var context = CreateContext(LocalTestAuth.BearerToken, IPAddress.Parse(remoteIp), host);

        LocalTestAuth.Evaluate(context, new LocalTestAuthOptions { Enabled = true }, DevelopmentEnvironment)
            .Should().Be(LocalTestAuthDecision.NonLoopback);
    }

    [Fact]
    public void Evaluate_WrongBearerToken_IsNotLocalAuthentication()
    {
        var context = CreateContext("someone-else", IPAddress.Loopback, "localhost");

        LocalTestAuth.Evaluate(context, new LocalTestAuthOptions { Enabled = true }, DevelopmentEnvironment)
            .Should().Be(LocalTestAuthDecision.NotRequested);
        LocalTestAuth.ShouldUseLocalHandler(context, new LocalTestAuthOptions { Enabled = true }, DevelopmentEnvironment)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public async Task AddCognitoAuthentication_WhenLocalModeIsUnavailable_DoesNotRegisterLocalScheme(string environmentName, bool enabled)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AWS:Region"] = "us-west-2",
            ["AWS:CognitoUserPoolId"] = "pool",
            ["AWS:CognitoClientId"] = "client",
            ["LocalTestAuth:Enabled"] = enabled.ToString()
        }).Build();
        services.AddCognitoAuthentication(configuration, new TestEnvironment { EnvironmentName = environmentName });
        using var provider = services.BuildServiceProvider();

        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        (await schemes.GetSchemeAsync(LocalTestAuth.Scheme)).Should().BeNull();
        (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name.Should().Be("Bearer");
    }

    private static readonly IHostEnvironment DevelopmentEnvironment = new TestEnvironment { EnvironmentName = Environments.Development };
    private static readonly IHostEnvironment ProductionEnvironment = new TestEnvironment { EnvironmentName = Environments.Production };

    private static DefaultHttpContext CreateContext(string token, IPAddress remoteIp, string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Request.Host = HostString.FromUriComponent(host);
        context.Connection.RemoteIpAddress = remoteIp;
        return context;
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "MediaRankerServer";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
