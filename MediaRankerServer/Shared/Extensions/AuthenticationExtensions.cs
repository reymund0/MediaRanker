using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MediaRankerServer.Shared.Authentication;

namespace MediaRankerServer.Shared.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddCognitoAuthentication(this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        var region = config["AWS:Region"];
        var userPoolId = config["AWS:CognitoUserPoolId"];
        var clientId = config["AWS:CognitoClientId"];
        var authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";

        var localTestAuth = config.GetSection(LocalTestAuthOptions.SectionPath).Get<LocalTestAuthOptions>() ?? new LocalTestAuthOptions();
        services.Configure<LocalTestAuthOptions>(config.GetSection(LocalTestAuthOptions.SectionPath));
        var useLocalTestAuth = localTestAuth.Enabled && environment.IsDevelopment();

        var authentication = services.AddAuthentication(useLocalTestAuth
            ? LocalTestAuth.PolicyScheme
            : JwtBearerDefaults.AuthenticationScheme);

        if (useLocalTestAuth)
        {
            authentication.AddPolicyScheme(LocalTestAuth.PolicyScheme, "Cognito or local test authentication", policy =>
            {
                policy.ForwardDefaultSelector = context => LocalTestAuth.ShouldUseLocalHandler(context, localTestAuth, environment)
                    ? LocalTestAuth.Scheme
                    : JwtBearerDefaults.AuthenticationScheme;
            });
        }

        authentication.AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = clientId,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    NameClaimType = "sub"
                };
            });

        if (useLocalTestAuth)
        {
            authentication.AddScheme<AuthenticationSchemeOptions, LocalTestAuthHandler>(LocalTestAuth.Scheme, _ => { });
        }

        return services;
    }
}
