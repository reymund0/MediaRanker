using Microsoft.Extensions.DependencyInjection;
using MediaRankerServer.Modules.Files.Services;
using Microsoft.Extensions.Configuration;
using FluentValidation;
using MediaRankerServer.Modules.Files.Contracts;

namespace MediaRankerServer.Modules.Files;

public static class FilesModule
{
    public static IServiceCollection AddFilesModule(this IServiceCollection services)
    {
        services.AddScoped<S3FileService>();
        services.AddScoped<IFileService>(sp => sp.GetRequiredService<S3FileService>());
        services.AddScoped<IFileCleanupService>(sp => sp.GetRequiredService<S3FileService>());
        services.AddScoped<IValidator<StartUploadRequest>>(sp => new StartUploadRequestValidator());
        services.AddScoped<IValidator<FinishUploadRequest>>(sp => new FinishUploadRequestValidator());
        
        return services;
    }
}
