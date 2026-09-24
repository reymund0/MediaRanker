using FluentAssertions;
using MediaRankerServer.Modules.Media;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class CatalogBootstrapOptionsTests
{
    [Fact]
    public void IgdbStatementTimeoutDefaultsToFifteenSeconds()
    {
        var options = new IgdbOptions();
        options.MaxStatementSeconds.Should().Be(15);
        options.HasValidLimits.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(15, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void IgdbStatementTimeoutMustBeBetweenOneAnd120Seconds(int seconds, bool valid)
    {
        new IgdbOptions { MaxStatementSeconds = seconds }.HasValidLimits.Should().Be(valid);
    }

    [Fact]
    public void EmptyConfigurationLeavesBootstrapDisabled()
    {
        CatalogBootstrapOptions.Read(new ConfigurationBuilder().Build()).Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("true", "false")]
    [InlineData("false", "true")]
    public void ConflictingRawAliasesFailBeforeBinding(string enabled, string importEnabled)
    {
        var config = Configuration(new() { ["Media:Igdb:Enabled"] = enabled, ["Media:Igdb:ImportEnabled"] = importEnabled });
        var resolve = () => CatalogBootstrapOptions.ResolveIgdbImportEnabled(config);
        resolve.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData("true", null, true)]
    [InlineData(null, "true", true)]
    [InlineData("false", "false", false)]
    [InlineData("true", "true", true)]
    public void OneAliasOrConsistentAliasesResolve(string? enabled, string? importEnabled, bool expected)
    {
        CatalogBootstrapOptions.ResolveIgdbImportEnabled(Configuration(new()
        {
            ["Media:Igdb:Enabled"] = enabled, ["Media:Igdb:ImportEnabled"] = importEnabled
        })).Should().Be(expected);
    }

    [Fact]
    public void EffectiveRawValuesRespectConfigurationPrecedence()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Media:Igdb:Enabled"] = "false", ["Media:Igdb:ImportEnabled"] = "false"
        }).AddCommandLine(["--Media:Igdb:Enabled=true", "--Media:Igdb:ImportEnabled=true"]).Build();
        CatalogBootstrapOptions.ResolveIgdbImportEnabled(config).Should().BeTrue();
    }

    [Fact]
    public void PersistedBootstrapCannotActivateEvenWithFiniteAllowances()
    {
        var read = () => CatalogBootstrapOptions.Read(Configuration(LaunchValues()));
        read.Should().Throw<OptionsValidationException>().WithMessage("*command line*");
    }

    [Fact]
    public void SelectorAloneCannotReusePersistedAllowances()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(LaunchValues())
            .AddCommandLine(["--Media:Bootstrap:Provider=igdb"]).Build();
        var read = () => CatalogBootstrapOptions.Read(config);
        read.Should().Throw<OptionsValidationException>().WithMessage("*MaxHttpAttempts*");
    }

    [Fact]
    public void CompleteExplicitLaunchAcceptsFiniteAllowance()
    {
        var config = new ConfigurationBuilder().AddCommandLine(LaunchValues()
            .Select(x => $"--{x.Key}={x.Value}").ToArray()).Build();
        var options = CatalogBootstrapOptions.Read(config);
        options.Provider.Should().Be("igdb");
        options.MaxHttpAttempts.Should().Be(100);
        options.MaxAdmissionRows.Should().Be(5000);
        options.MaxSeconds.Should().Be(900);
    }

    [Fact]
    public void SimultaneousProviderSelectionIsRejected()
    {
        var read = () => CatalogBootstrapOptions.Read(new ConfigurationBuilder()
            .AddCommandLine(["--Media:Bootstrap:Provider=igdb,imdb"]).Build());
        read.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void LaterConfigurationCannotIncreaseExplicitAllowance()
    {
        var config = new ConfigurationBuilder().AddCommandLine(LaunchValues()
            .Select(x => $"--{x.Key}={x.Value}").ToArray())
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Media:Bootstrap:MaxHttpAttempts"] = "101" }).Build();
        var read = () => CatalogBootstrapOptions.Read(config);
        read.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void LiveImdbRequiresOperatorConfirmedProfile(bool enabled, bool confirmed, bool valid)
    {
        var config = Configuration(new()
        {
            ["Media:ImdbImport:Enabled"] = enabled.ToString(),
            ["Media:ImdbImport:CalibratedProfileConfirmed"] = confirmed.ToString()
        });
        using var services = new ServiceCollection().AddLogging().AddMediaModule(config,
            Mock.Of<IHostEnvironment>(x => x.EnvironmentName == "Testing")).BuildServiceProvider();
        var resolve = () => services.GetRequiredService<IOptions<ImdbImportOptions>>().Value;
        if (valid) resolve.Should().NotThrow();
        else resolve.Should().Throw<OptionsValidationException>().WithMessage("*calibrated*");
    }

    [Fact]
    public void ImdbBootstrapCannotReuseSavedSessionLimits()
    {
        var values = ImdbLaunchValues();
        var config = new ConfigurationBuilder().AddInMemoryCollection(values)
            .AddCommandLine(["--Media:Bootstrap:Provider=imdb"]).Build();
        var read = () => CatalogBootstrapOptions.Read(config);
        read.Should().Throw<OptionsValidationException>().WithMessage("*MaxHttpAttempts*");
    }

    [Fact]
    public void ImdbBootstrapAcceptsAllSixExplicitSessionLimits()
    {
        var config = new ConfigurationBuilder().AddCommandLine(ImdbLaunchValues()
            .Select(x => $"--{x.Key}={x.Value}").ToArray()).Build();
        CatalogBootstrapOptions.Read(config).Provider.Should().Be("imdb");
    }

    private static Dictionary<string, string?> ImdbLaunchValues() => new()
    {
        ["Media:Bootstrap:Provider"] = "imdb", ["Media:ImdbImport:Enabled"] = "true",
        ["Media:ImdbImport:MaxHttpAttempts"] = "3", ["Media:ImdbImport:MaxCompressedBytesPerFeed"] = "1048576",
        ["Media:ImdbImport:MaxTemporaryDiskBytes"] = "1048576", ["Media:ImdbImport:MaxDecompressedBytesPerFeed"] = "10485760",
        ["Media:ImdbImport:MaxWholeSessionSeconds"] = "900", ["Media:ImdbImport:MaxRowsPerFeed"] = "1000"
    };

    private static IConfiguration Configuration(Dictionary<string, string?> values) => new ConfigurationBuilder()
        .AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> LaunchValues() => new()
    {
        ["Media:Bootstrap:Provider"] = "igdb", ["Media:Igdb:ImportEnabled"] = "true",
        ["Media:Bootstrap:MaxHttpAttempts"] = "100", ["Media:Bootstrap:MaxAdmissionRows"] = "5000",
        ["Media:Bootstrap:MaxSeconds"] = "900"
    };
}
