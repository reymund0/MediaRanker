using FluentValidation;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<ImdbImportService>();
        services.AddScoped<IValidator<MediaUpsertRequest>, MediaUpsertRequestValidator>();
        services.AddScoped<IValidator<GenerateUploadCoverUrlRequest>, GenerateUploadCoverUrlRequestValidator>();

        services.AddHttpClient<ImdbTsvProvider>();

        if (!environment.IsEnvironment("Testing"))
        {
            // TODO: Re-enable this when we're ready to run the job
            // services.AddHostedService<ImdbImportJob>();
        }

        return services;
    }
}
