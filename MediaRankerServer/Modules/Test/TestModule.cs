using Microsoft.Extensions.DependencyInjection;
using MediaRankerServer.Modules.Media.Services;

namespace MediaRankerServer.Modules.Test;

public static class TestModule
{
    public static IServiceCollection AddTestModule(this IServiceCollection services)
    {
        services.AddScoped<ImdbImportService>();
        services.AddScoped<ImdbLoadService>();
        return services;
    }
}
