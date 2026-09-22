using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Providers;

public static class MediaProviderServiceCollectionExtensions
{
    public const string IgdbTwitchClientName = "MediaRanker.IGDB.Twitch";

    public static IServiceCollection AddMediaProviders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IgdbOptions>()
            .Bind(configuration.GetSection(IgdbOptions.SectionPath))
            .Validate(static options => options.HasValidLimits, "Invalid IGDB provider request budget or schedule.");
        services.AddOptions<TmdbOptions>()
            .Bind(configuration.GetSection(TmdbOptions.SectionPath))
            .Validate(static options => options.HasValidLimits, "Invalid TMDB provider timeout.");

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<IgdbOptions>>().Value);
        services.AddSingleton<IgdbRequestLimiter>();
        services.AddSingleton(sp => new TmdbRequestCooldown(sp.GetRequiredService<IOptions<TmdbOptions>>().Value));
        services.AddHttpClient<IgdbClient>((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://api.igdb.com/v4/");
            client.Timeout = TimeSpan.FromSeconds(serviceProvider.GetRequiredService<IgdbOptions>().TimeoutSeconds);
        });
        services.AddSingleton<IIgdbClient>(sp => sp.GetRequiredService<IgdbClient>());
        services.AddHttpClient(IgdbTwitchClientName, (serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://id.twitch.tv/");
            client.Timeout = TimeSpan.FromSeconds(serviceProvider.GetRequiredService<IgdbOptions>().TimeoutSeconds);
        });
        services.AddHttpClient<ITmdbClient, TmdbClient>((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.Timeout = TimeSpan.FromSeconds(serviceProvider.GetRequiredService<IOptions<TmdbOptions>>().Value.TimeoutSeconds);
        });
        return services;
    }
}
