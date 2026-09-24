using FluentAssertions;
using MediaRankerServer.Modules.Media.Jobs;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ImdbImportOptionsTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(604_800, true)]
    [InlineData(0, false)]
    [InlineData(604_801, false)]
    public void WholeSessionDurationMustFitTheSupportedFiniteRange(int seconds, bool expectedValid)
    {
        var options = new ImdbImportOptions { MaxWholeSessionSeconds = seconds };

        options.HasFiniteProfile(out _).Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(23, true)]
    [InlineData(-1, false)]
    [InlineData(24, false)]
    public void ScheduleHourMustBeWithinUtcDay(int hour, bool expectedValid)
    {
        var options = new ImdbImportOptions { ScheduleHourUtc = hour };

        options.HasFiniteProfile(out _).Should().Be(expectedValid);
    }

    [Fact]
    public void BatchCharacterLimitHasAFiniteDefaultAndUpperBound()
    {
        new ImdbImportOptions().MaxBatchCharacters.Should().Be(8 * 1024 * 1024);
        new ImdbImportOptions { MaxBatchCharacters = ImdbImportOptions.MaximumBatchCharacters }
            .HasFiniteProfile(out _).Should().BeTrue();
        new ImdbImportOptions { MaxBatchCharacters = ImdbImportOptions.MaximumBatchCharacters + 1 }
            .HasFiniteProfile(out _).Should().BeFalse();
    }

    [Fact]
    public void BatchRowLimitHasAFiniteDefaultAndUpperBound()
    {
        new ImdbImportOptions().BatchSize.Should().Be(ImdbImportOptions.DefaultBatchSize);
        new ImdbImportOptions { BatchSize = ImdbImportOptions.MaximumBatchSize }
            .HasFiniteProfile(out _).Should().BeTrue();
        new ImdbImportOptions { BatchSize = ImdbImportOptions.MaximumBatchSize + 1 }
            .HasFiniteProfile(out _).Should().BeFalse();
        new ImdbImportOptions { BatchSize = int.MaxValue }
            .HasFiniteProfile(out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(12, 12, true)]
    [InlineData(13, 12, false)]
    [InlineData(0, 12, false)]
    public void MaximumLineMustFitWithinTheAggregateBatchCharacterLimit(
        int maximumLineCharacters,
        int maximumBatchCharacters,
        bool expectedValid)
    {
        var options = new ImdbImportOptions
        {
            MaxLineCharacters = maximumLineCharacters,
            MaxBatchCharacters = maximumBatchCharacters
        };

        options.HasFiniteProfile(out _).Should().Be(expectedValid);
    }
}
