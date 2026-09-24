using Microsoft.Extensions.Options;
using MediaRankerServer.Modules.Media.Jobs;

namespace MediaRankerServer.Modules.Media.Providers;

public static class MediaProviderServiceCollectionExtensions
{
    public const string IgdbTwitchClientName = "MediaRanker.IGDB.Twitch";

    public static IServiceCollection AddMediaProviders(this IServiceCollection services, IConfiguration configuration)
    {
        var effectiveImportEnabled = CatalogBootstrapOptions.ResolveIgdbImportEnabled(configuration);
        services.AddOptions<IgdbOptions>()
            .Bind(configuration.GetSection(IgdbOptions.SectionPath)).Configure(options => { if (effectiveImportEnabled.HasValue) options.ImportEnabled = effectiveImportEnabled.Value; })
            .Validate(static options => options.HasValidLimits, "Invalid IGDB provider request budget or schedule.").ValidateOnStart();
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
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IIgdbClient>(sp => sp.GetRequiredService<IgdbClient>());
        services.AddHttpClient(IgdbTwitchClientName, (serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://id.twitch.tv/");
            client.Timeout = TimeSpan.FromSeconds(serviceProvider.GetRequiredService<IgdbOptions>().TimeoutSeconds);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<ITmdbClient, TmdbClient>((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.Timeout = TimeSpan.FromSeconds(serviceProvider.GetRequiredService<IOptions<TmdbOptions>>().Value.TimeoutSeconds);
        });
        return services;
    }
}
