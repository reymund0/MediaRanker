using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Providers;

namespace MediaRankerServer.Modules.Media;

public static class MediaModule
{
    public static IServiceCollection AddMediaModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<ImdbImportOptions>(configuration.GetSection(ImdbImportOptions.SectionPath));
        services.AddOptions<ArtworkOptions>().Bind(configuration.GetSection(ArtworkOptions.SectionPath))
            .Validate(x => x.IsValid(), "Invalid artwork timing or cache policy.").ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddMediaProviders(configuration);

        services.AddScoped<IArtworkService, ArtworkService>();
        services.AddScoped<ArtworkProcessor>();
        services.AddScoped<IIgdbImportProvider, IgdbImportSqlProvider>();
        services.AddScoped<IgdbImportService>();
        services.AddScoped<IImdbImportProvider, ImdbImportSqlProvider>();
        services.AddScoped<IImdbLoadProvider, ImdbLoadSqlProvider>();
        services.AddScoped<ImdbLoadService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaCollectionService, MediaCollectionService>();
        services.AddScoped<ImdbImportService>();
        services.AddScoped<IValidator<MediaUpsertRequest>, MediaUpsertRequestValidator>();
        services.AddScoped<IValidator<MediaCollectionUpsertRequest>, MediaCollectionUpsertRequestValidator>();

        services.AddHttpClient<ImdbTsvProvider>();

        if (!environment.IsEnvironment("Testing") && !environment.IsEnvironment("Integration"))
        {
            services.AddHostedService<ImdbImportJob>();
            services.AddHostedService<ArtworkJob>();
            services.AddHostedService<IgdbImportJob>();
        }

        return services;
    }
}
