using System.Collections.Generic;
using System.Linq;
using MediaRankerServer.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MediaRankerServer.Shared.Data;
using Serilog;

namespace MediaRankerServer.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory<TProgram>(string connectionString) : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly string _connectionString = connectionString;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    path: "../../../logs/integration-tests-.txt",
                    rollingInterval: RollingInterval.Minute,
                    retainedFileCountLimit: 10,
                    shared: true
                );
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Integration");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.Integration.json", optional: true);
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove real DB context
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PostgreSQLContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Add test DB context
            services.AddDbContext<PostgreSQLContext>(options =>
            {
                options.UseNpgsql(_connectionString);
            });

            // Replace Auth with Test Scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, _ => { });
        });
    }
}
