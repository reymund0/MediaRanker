using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Shared.Authentication;

public sealed class LocalTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<LocalTestAuthOptions> options,
    IHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var decision = LocalTestAuth.Evaluate(Context, options.Value, environment);
        if (decision == LocalTestAuthDecision.NotRequested)
            return Task.FromResult(AuthenticateResult.NoResult());
        if (decision != LocalTestAuthDecision.Accepted)
            return Task.FromResult(AuthenticateResult.Fail("Local test authentication is unavailable."));

        var ticket = new AuthenticationTicket(LocalTestAuth.CreatePrincipal(), LocalTestAuth.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
