using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

/// <summary>One deliberate launch grants one process-local bootstrap allowance.</summary>
public sealed class CatalogBootstrapOptions
{
    public const string SectionPath = "Media:Bootstrap";
    public string Provider { get; set; } = "none";
    public int MaxHttpAttempts { get; set; }
    public int MaxAdmissionRows { get; set; }
    public int MaxSeconds { get; set; }
    public int IgdbYieldMilliseconds { get; set; } = 1000;
    public bool Enabled => Provider != "none";

    public static CatalogBootstrapOptions Read(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionPath).Get<CatalogBootstrapOptions>() ?? new();
        options.Provider = options.Provider.Trim().ToLowerInvariant();
        if (options.Provider is not ("none" or "igdb" or "imdb"))
            Fail("Select exactly one bootstrap provider: none, igdb or imdb.");
        if (!options.Enabled) return options;

        RequireLaunchArgument(configuration, $"{SectionPath}:Provider");
        if (options.Provider == "igdb")
        {
            foreach (var key in new[] { "MaxHttpAttempts", "MaxAdmissionRows", "MaxSeconds" })
                RequireLaunchArgument(configuration, $"{SectionPath}:{key}");
            if (options.MaxHttpAttempts <= 0 || options.MaxAdmissionRows <= 0 || options.MaxSeconds is <= 0 or > 604800)
                Fail("IGDB bootstrap requires positive finite HTTP, admission and elapsed-time allowances (at most seven days).");
            if (options.IgdbYieldMilliseconds is < 1000 or > 60000)
                Fail("IGDB bootstrap yield must be between 1000 and 60000 milliseconds.");
            if (ResolveIgdbImportEnabled(configuration) != true)
                Fail("IGDB bootstrap requires Media:Igdb:ImportEnabled (or its Enabled alias).");
        }
        else
        {
            foreach (var key in new[] { "MaxHttpAttempts", "MaxCompressedBytesPerFeed", "MaxTemporaryDiskBytes",
                         "MaxDecompressedBytesPerFeed", "MaxWholeSessionSeconds", "MaxRowsPerFeed" })
                RequireLaunchArgument(configuration, $"Media:ImdbImport:{key}");
            if (!configuration.GetValue<bool>("Media:ImdbImport:Enabled"))
                Fail("IMDb bootstrap requires Media:ImdbImport:Enabled.");
        }
        return options;
    }

    // Inspect effective raw keys before binding: both setters otherwise overwrite the same field.
    public static bool? ResolveIgdbImportEnabled(IConfiguration configuration)
    {
        static bool? Parse(IConfiguration config, string key)
        {
            var raw = config[key];
            if (raw is null) return null;
            if (bool.TryParse(raw, out var value)) return value;
            Fail($"{key} must be true or false.");
            return null;
        }
        var enabled = Parse(configuration, "Media:Igdb:Enabled");
        var importEnabled = Parse(configuration, "Media:Igdb:ImportEnabled");
        if (enabled.HasValue && importEnabled.HasValue && enabled != importEnabled)
            Fail("Media:Igdb:Enabled and Media:Igdb:ImportEnabled conflict; set both effective values consistently.");
        return importEnabled ?? enabled;
    }

    private static void RequireLaunchArgument(IConfiguration configuration, string key)
    {
        var supplied = configuration is IConfigurationRoot root && root.Providers
            .OfType<CommandLineConfigurationProvider>()
            .Reverse().Any(provider => provider.TryGet(key, out var value)
                && string.Equals(value, configuration[key], StringComparison.OrdinalIgnoreCase));
        if (!supplied)
            Fail($"Bootstrap requires {key} explicitly on this launch's command line; persisted configuration cannot grant an allowance.");
    }

    private static void Fail(string message) => throw new OptionsValidationException(
        nameof(CatalogBootstrapOptions), typeof(CatalogBootstrapOptions), [message]);
}
