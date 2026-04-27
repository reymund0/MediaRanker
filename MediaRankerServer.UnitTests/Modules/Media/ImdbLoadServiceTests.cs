using FluentAssertions;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ImdbLoadServiceTests
{
    private readonly Mock<IImdbLoadProvider> _mockProvider;
    private readonly Mock<ILogger<ImdbLoadService>> _mockLogger;
    private readonly ImdbLoadService _service;

    public ImdbLoadServiceTests()
    {
        _mockProvider = new Mock<IImdbLoadProvider>();
        _mockLogger = new Mock<ILogger<ImdbLoadService>>();
        _service = new ImdbLoadService(_mockProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task LoadNonSeriesMediaAsync_DelegatesToProvider()
    {
        _mockProvider
            .Setup(p => p.LoadNonSeriesMediaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImdbLoadResult(42));

        var result = await _service.LoadNonSeriesMediaAsync();

        result.Affected.Should().Be(42);
        _mockProvider.Verify(p => p.LoadNonSeriesMediaAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_ReturnsResultFromLoadNonSeriesMediaAsync()
    {
        _mockProvider
            .Setup(p => p.LoadNonSeriesMediaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImdbLoadResult(7));

        var result = await _service.LoadAsync();

        result.Affected.Should().Be(7);
    }
}
