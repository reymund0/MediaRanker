using MediaRankerServer.Modules.Media.Services;

namespace MediaRankerServer.Modules.Media;

public static class MediaModule
{
    public static IServiceCollection AddMediaModule(this IServiceCollection services)
    {
        services.AddScoped<IMediaService, MediaService>();

        return services;
    }
}
