using Microsoft.Extensions.DependencyInjection;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Files.Jobs;
using Microsoft.Extensions.Configuration;
using FluentValidation;
using MediaRankerServer.Modules.Files.Contracts;
using Microsoft.Extensions.Hosting;

namespace MediaRankerServer.Modules.Files;

public static class FilesModule
{
    public static IServiceCollection AddFilesModule(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<FileCleanupOptions>(configuration.GetSection(FileCleanupOptions.SectionPath));

        services.AddScoped<S3FileService>();
        services.AddScoped<IFileService>(sp => sp.GetRequiredService<S3FileService>());
        services.AddScoped<IFileCleanupService>(sp => sp.GetRequiredService<S3FileService>());
        services.AddScoped<IValidator<StartUploadRequest>>(sp => new StartUploadRequestValidator());
        services.AddScoped<IValidator<FinishUploadRequest>>(sp => new FinishUploadRequestValidator());

        if (!environment.IsEnvironment("Testing"))
        {
            services.AddHostedService<FileUploadCleanupJob>();
        }

        return services;
    }
}
