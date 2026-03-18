using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Services;

namespace MediaRankerServer.Modules.Media;

public static class MediaModule
{
    public static IServiceCollection AddMediaModule(this IServiceCollection services)
    {
        services.AddScoped<MediaCoverStorageService>();
        services.AddScoped<IMediaCoverService, MediaCoverService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IValidator<MediaUpsertRequest>, MediaUpsertRequestValidator>();

        return services;
    }
}
