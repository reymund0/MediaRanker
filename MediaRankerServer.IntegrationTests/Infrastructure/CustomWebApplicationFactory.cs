using System.Collections.Generic;
using System.Linq;
using Amazon.Runtime;
using Amazon.S3;
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

public class CustomWebApplicationFactory<TProgram>(string connectionString, string s3ServiceUrl) : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly string _connectionString = connectionString;
    private readonly string _s3ServiceUrl = s3ServiceUrl;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:Region"] = LocalStackContainerFixture.AwsRegion
            });
        });

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
            var testAssemblyPath = Path.GetDirectoryName(typeof(CustomWebApplicationFactory<TProgram>).Assembly.Location)!;
            config.SetBasePath(testAssemblyPath);
            config.AddJsonFile("appsettings.Integration.json", optional: false);
            
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

            // Replace AWS S3 with LocalStack-backed client
            var s3Descriptors = services.Where(d => d.ServiceType == typeof(IAmazonS3)).ToList();
            foreach (var s3Descriptor in s3Descriptors)
            {
                services.Remove(s3Descriptor);
            }

            services.AddSingleton<IAmazonS3>(_ =>
            {
                var credentials = new BasicAWSCredentials(
                    LocalStackContainerFixture.AwsAccessKeyId,
                    LocalStackContainerFixture.AwsSecretAccessKey);

                var config = new AmazonS3Config
                {
                    ServiceURL = _s3ServiceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = LocalStackContainerFixture.AwsRegion
                };

                return new AmazonS3Client(credentials, config);
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
