using FluentAssertions;
using FluentValidation;
using MediatR;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.UnitTests.Shared;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class MediaServiceTests : IDisposable
{
    private readonly PostgreSQLContext _context;
    private readonly Mock<IArtworkService> _mockArtworkService;
    private readonly Mock<IValidator<MediaUpsertRequest>> _mockValidator;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly MediaService _service;
    private const string DefaultUserId = "test-user-1";

    public MediaServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _mockArtworkService = new Mock<IArtworkService>();
        _mockArtworkService
            .Setup(service => service.GetMediaArtworkAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, CoverPresentation>());

        _mockValidator = new Mock<IValidator<MediaUpsertRequest>>();
        _mockValidator.Setup(v => v.Validate(It.IsAny<MediaUpsertRequest>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _mockPublisher = new Mock<IPublisher>();

        _service = new MediaService(_context, _mockArtworkService.Object, _mockValidator.Object, _mockPublisher.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetMediaByIdAsync_UsesArtworkPresentationForReturnedMedia()
    {
        var media = new MediaEntity
        {
            Title = "The Matrix",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(1999, 3, 31)
        };
        _context.Media.Add(media);
        await _context.SaveChangesAsync();
        _mockArtworkService
            .Setup(service => service.GetMediaArtworkAsync(
                It.Is<IEnumerable<long>>(ids => ids.SequenceEqual(new[] { media.Id })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, CoverPresentation>
            {
                [media.Id] = new("https://image.tmdb.org/t/p/w342/matrix.jpg", "ready")
            });

        var result = await _service.GetMediaByIdAsync(media.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CoverImageUrl.Should().Be("https://image.tmdb.org/t/p/w342/matrix.jpg");
        result.CoverStatus.Should().Be("ready");
    }

    [Fact]
    public async Task GetMediaByIdAsync_WhenArtworkIsNotRequested_DoesNotCreateArtworkDemand()
    {
        var media = new MediaEntity
        {
            Title = "Review Validation Movie",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(1999, 3, 31),
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt0133093"
        };
        _context.Media.Add(media);
        await _context.SaveChangesAsync();

        var result = await _service.GetMediaByIdAsync(media.Id, CancellationToken.None, requestArtwork: false);

        result.Should().NotBeNull();
        result!.CoverStatus.Should().Be("unsupported");
        _mockArtworkService.Verify(
            service => service.GetMediaArtworkAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateMediaAsync_WhenDuplicateExists_ThrowsDomainException()
    {
        // Arrange
        _context.Media.Add(new MediaEntity
        {
            Id = 1,
            Title = "Inception",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(2010, 7, 16),
        });
        await _context.SaveChangesAsync();

        var act = () => _service.CreateMediaAsync(DefaultUserId, new MediaUpsertRequest
        {
            Title = "Inception",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(2010, 7, 16),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "media_conflict");
    }

    [Fact]
    public async Task UpdateMediaAsync_WhenMediaMissing_ThrowsDomainException()
    {
        var act = () => _service.UpdateMediaAsync(DefaultUserId, 999, new MediaUpsertRequest
        {
            Title = "Unknown",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "media_not_found");
    }

    [Fact]
    public async Task DeleteMediaAsync_WhenMediaMissing_ThrowsDomainException()
    {
        var act = () => _service.DeleteMediaAsync(999);

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "media_not_found");
    }

    [Fact]
    public async Task CreateMediaAsync_WhenValidationFails_ThrowsValidationDomainException()
    {
        _mockValidator.Setup(v => v.Validate(It.IsAny<MediaUpsertRequest>()))
            .Returns(new FluentValidation.Results.ValidationResult([
                new FluentValidation.Results.ValidationFailure("Title", "Media title is required."),
            ]));

        var act = () => _service.CreateMediaAsync(DefaultUserId, new MediaUpsertRequest
        {
            Title = "",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "media_validation_error");
    }

    [Fact]
    public async Task UpdateMediaAsync_WithoutUpload_PreservesAutomaticCoverAssociation()
    {
        var cover = new MediaCover
        {
            Id = 100,
            Provider = ArtworkProvider.Tmdb,
            LookupKind = CoverLookupKind.MovieImdb,
            LookupId = "tt0816692",
            Outcome = CoverOutcome.Ready,
            ImagePath = "/interstellar.jpg",
            CheckedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };
        _context.MediaCovers.Add(cover);
        var existingMedia = new MediaEntity
        {
            Title = "Interstellar",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(2014, 11, 7),
            CoverId = cover.Id
        };
        _context.Media.Add(existingMedia);
        await _context.SaveChangesAsync();

        var request = new MediaUpsertRequest
        {
            Id = existingMedia.Id,
            Title = "Interstellar Updated",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(2014, 11, 7),
        };

        // Act
        await _service.UpdateMediaAsync(DefaultUserId, existingMedia.Id, request);

        // Assert
        var entity = await _context.Media.FirstAsync(m => m.Id == existingMedia.Id);
        entity.Title.Should().Be("Interstellar Updated");
        entity.CoverId.Should().Be(cover.Id);
    }

    [Fact]
    public async Task DeleteMediaAsync_DeletesMedia()
    {
        // Arrange
        var media = new MediaEntity
        {
            Title = "To Delete",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(2020, 1, 1),
            CoverId = 100
        };
        _context.Media.Add(media);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteMediaAsync(media.Id);

        // Assert
        _context.Media.Should().NotContain(m => m.Id == media.Id);
    }

    [Fact]
    public async Task DeleteMediaAsync_PublishesMediaDeletedEvent()
    {
        // Arrange
        var media = new MediaEntity
        {
            Title = "Event Test",
            MediaType = "Movie",
            ReleaseDate = new DateOnly(2021, 1, 1)
        };
        _context.Media.Add(media);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteMediaAsync(media.Id);

        // Assert
        _mockPublisher.Verify(
            p => p.Publish(It.Is<MediaDeletedEvent>(e => e.MediaId == media.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteMediaAsync_WhenMediaMissing_DoesNotPublishEvent()
    {
        // Act
        var act = () => _service.DeleteMediaAsync(999);
        await act.Should().ThrowAsync<DomainException>();

        // Assert
        _mockPublisher.Verify(
            p => p.Publish(It.IsAny<MediaDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // --- Validation tests ---

    [Fact]
    public async Task CreateMediaAsync_WhenMediaTypeIsUnknown_ThrowsDomainException()
    {
        // Arrange
        var request = new MediaUpsertRequest
        {
            Title = "New Movie",
            MediaType = "Unknown",
            ReleaseDate = new DateOnly(2020, 1, 1),
        };

        // Act
        var act = () => _service.CreateMediaAsync(DefaultUserId, request);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "media_type_not_found");
    }
}
