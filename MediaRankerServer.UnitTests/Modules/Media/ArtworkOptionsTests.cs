using FluentAssertions;
using MediaRankerServer.Modules.Media.Jobs;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ArtworkOptionsTests
{
    [Theory]
    [InlineData(0, 7, 120, 5)]
    [InlineData(151, 7, 120, 5)]
    [InlineData(30, 0, 120, 5)]
    [InlineData(30, 151, 120, 5)]
    [InlineData(30, 7, 59, 5)]
    [InlineData(30, 7, 120, 0)]
    public void IsValid_RejectsOutOfRangeCacheAndLeaseSettings(int positiveDays, int negativeDays, int leaseSeconds, int maxAttempts)
    {
        var options = new ArtworkOptions
        {
            PositiveCacheDays = positiveDays,
            NegativeCacheDays = negativeDays,
            LeaseSeconds = leaseSeconds,
            MaxAttempts = maxAttempts
        };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_AcceptsTheMaximumBoundedCacheRetention()
    {
        var options = new ArtworkOptions
        {
            PositiveCacheDays = 150,
            NegativeCacheDays = 150,
            LeaseSeconds = 600,
            MaxAttempts = 10,
            RetrySeconds = 60,
            MaxRetrySeconds = 86400
        };

        options.IsValid().Should().BeTrue();
    }
}
