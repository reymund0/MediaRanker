using Microsoft.Extensions.DependencyInjection;
using MediaRankerServer.Modules.Files.Services;
using Microsoft.Extensions.Configuration;

namespace MediaRankerServer.Modules.Files;

public static class FilesModule
{
    public static IServiceCollection AddFilesModule(this IServiceCollection services)
    {
        services.AddScoped<IFileService, S3FileService>();
        
        return services;
    }
}
