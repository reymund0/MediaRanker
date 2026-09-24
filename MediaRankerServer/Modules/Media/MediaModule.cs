using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media;

public static class MediaModule
{
    public static IServiceCollection AddMediaModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton(CatalogBootstrapOptions.Read(configuration));
        services.AddSingleton<CatalogScheduleGate>();
        services.AddOptions<ImdbImportOptions>().Bind(configuration.GetSection(ImdbImportOptions.SectionPath))
            .Validate(x => x.HasFiniteProfile(out _), "IMDb import requires a valid finite resource profile.")
            .Validate(x => !x.Enabled || x.CalibratedProfileConfirmed,
                "Live IMDb imports require an operator-reviewed calibrated profile; local fixture limits do not certify full-feed readiness.")
            .ValidateOnStart();
        services.AddOptions<ArtworkOptions>().Bind(configuration.GetSection(ArtworkOptions.SectionPath))
            .Validate(x => x.IsValid(), "Invalid artwork timing or cache policy.").ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddMediaProviders(configuration);

        services.AddScoped<IArtworkService, ArtworkService>();
        services.AddScoped<ArtworkProcessor>();
        services.AddScoped<IIgdbImportProvider, IgdbImportSqlProvider>();
        services.AddScoped<IgdbImportService>();
        // IMDb SQL contains provider values. Keep EF's raw command/exception logs
        // out of retained output; the providers emit sanitized stage/count logs.
        // The keyed context stays scoped and does not change other modules' logging.
        services.AddKeyedScoped<PostgreSQLContext>("imdb", (sp, _) => new PostgreSQLContext(
            new DbContextOptionsBuilder<PostgreSQLContext>(sp.GetRequiredService<DbContextOptions<PostgreSQLContext>>())
                .UseLoggerFactory(NullLoggerFactory.Instance).EnableSensitiveDataLogging(false)
                .EnableDetailedErrors(false).Options));
        services.AddScoped<IImdbImportProvider>(sp => new ImdbImportSqlProvider(
            sp.GetRequiredKeyedService<PostgreSQLContext>("imdb"), sp.GetRequiredService<ILogger<ImdbImportSqlProvider>>(),
            sp.GetRequiredService<IOptions<ImdbImportOptions>>()));
        services.AddScoped<IImdbLoadProvider>(sp => new ImdbLoadSqlProvider(
            sp.GetRequiredKeyedService<PostgreSQLContext>("imdb"), sp.GetRequiredService<ILogger<ImdbLoadSqlProvider>>(),
            sp.GetRequiredService<IOptions<ImdbImportOptions>>()));
        services.AddScoped(sp => new ImdbLoadService(sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImdbImportOptions>>(),
            sp.GetRequiredService<ILogger<ImdbLoadService>>()));
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaCollectionService, MediaCollectionService>();
        services.AddScoped(sp => new ImdbImportService(sp.GetRequiredService<ImdbTsvProvider>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImdbImportOptions>>(),
            sp.GetRequiredService<ILogger<ImdbImportService>>()));
        services.AddScoped<IValidator<MediaUpsertRequest>, MediaUpsertRequestValidator>();
        services.AddScoped<IValidator<MediaCollectionUpsertRequest>, MediaCollectionUpsertRequestValidator>();

        services.AddHttpClient<ImdbTsvProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        if (!environment.IsEnvironment("Testing") && !environment.IsEnvironment("Integration"))
        {
            services.AddHostedService<ImdbImportJob>();
            services.AddHostedService<ArtworkJob>();
            services.AddHostedService<IgdbImportJob>();
        }

        return services;
    }
}
