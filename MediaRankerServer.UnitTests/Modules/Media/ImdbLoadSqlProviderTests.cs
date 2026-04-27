using FluentAssertions;
using MediaRankerServer.Modules.Media.Data;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ImdbLoadSqlProviderTests
{
    // Access internal helpers via InternalsVisibleTo
    private static readonly IReadOnlyDictionary<string, long> Map =
        ImdbLoadSqlProvider.NonSeriesTitleTypeMap;

    // --- NonSeriesTitleTypeMap ---

    [Theory]
    [InlineData("videoGame", -1L)]
    [InlineData("movie", -3L)]
    [InlineData("tvMovie", -3L)]
    [InlineData("short", -3L)]
    [InlineData("tvShort", -3L)]
    [InlineData("video", -3L)]
    public void NonSeriesTitleTypeMap_ContainsExpectedMapping(string titleType, long expectedMediaTypeId)
    {
        Map.Should().ContainKey(titleType);
        Map[titleType].Should().Be(expectedMediaTypeId);
    }

    [Theory]
    [InlineData("tvSeries")]
    [InlineData("tvMiniSeries")]
    [InlineData("tvEpisode")]
    [InlineData("tvPilot")]
    public void NonSeriesTitleTypeMap_DoesNotContainSeriesTypes(string titleType)
    {
        Map.Should().NotContainKey(titleType);
    }

    // --- BuildCaseClause ---

    [Fact]
    public void BuildCaseClause_ContainsAllTitleTypes()
    {
        var result = ImdbLoadSqlProvider.BuildCaseClause(Map);

        result.Should().Contain("CASE i.title_type");
        result.Should().Contain("WHEN 'videoGame' THEN -1");
        result.Should().Contain("WHEN 'movie' THEN -3");
        result.Should().Contain("WHEN 'tvMovie' THEN -3");
        result.Should().Contain("WHEN 'short' THEN -3");
        result.Should().Contain("WHEN 'tvShort' THEN -3");
        result.Should().Contain("WHEN 'video' THEN -3");
        result.Should().Contain("END");
    }

    // --- BuildInClause ---

    [Fact]
    public void BuildInClause_ContainsAllEligibleTitleTypes()
    {
        var result = ImdbLoadSqlProvider.BuildInClause(Map);

        result.Should().Contain("'videoGame'");
        result.Should().Contain("'movie'");
        result.Should().Contain("'tvMovie'");
        result.Should().Contain("'short'");
        result.Should().Contain("'tvShort'");
        result.Should().Contain("'video'");
        result.Should().NotContain("tvSeries");
    }
}
