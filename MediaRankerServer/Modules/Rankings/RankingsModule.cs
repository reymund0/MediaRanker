using FluentValidation;
using MediaRankerServer.Modules.Rankings.Contracts;
using MediaRankerServer.Modules.Rankings.Services;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Modules.Media.Services;


namespace MediaRankerServer.Modules.Rankings;

public static class RankingsModule
{
    public static IServiceCollection AddRankingsModule(this IServiceCollection services)
    {
        // Placeholder for future Rankings services
        services.AddScoped<IRankedMediaService, RankedMediaService>();
        services.AddScoped<ITemplatesService, TemplatesService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IValidator<RankedMediaUpsertRequest>, RankedMediaUpsertRequestValidator>();
        return services;
    }
}
