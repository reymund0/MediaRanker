using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MediaRankerServer.Modules.Media.Services;

namespace MediaRankerServer.Modules.Test;

public static class TestModule
{
    public static IServiceCollection AddTestModule(this IServiceCollection services)
    {
        services.TryAddScoped<ImdbImportService>();
        services.TryAddScoped<ImdbLoadService>();
        return services;
    }
}
