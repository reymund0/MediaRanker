using FluentValidation;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Data;

namespace MediaRankerServer.Modules.Media;

public static class MediaModule
{
    public static IServiceCollection AddMediaModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<ImdbImportOptions>(configuration.GetSection(ImdbImportOptions.SectionName));

        services.AddScoped<IFileService, S3FileService>();
        services.AddScoped<IMediaCoverService, MediaCoverService>();
        services.AddScoped<IMediaCoverCleanupService, MediaCoverService>();
        services.AddScoped<IImdbImportProvider, ImdbImportSqlProvider>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaCollectionService, MediaCollectionService>();
        services.AddScoped<ImdbImportService>();
        services.AddScoped<IValidator<MediaUpsertRequest>, MediaUpsertRequestValidator>();
        services.AddScoped<IValidator<GenerateUploadCoverUrlRequest>, GenerateUploadCoverUrlRequestValidator>();
        services.AddScoped<IValidator<MediaCollectionUpsertRequest>, MediaCollectionUpsertRequestValidator>();

        services.AddHttpClient<ImdbTsvProvider>();

        if (!environment.IsEnvironment("Testing"))
        {
            services.AddHostedService<ImdbImportJob>();
            services.AddHostedService<MediaCoverCleanupJob>();
        }

        return services;
    }
}
