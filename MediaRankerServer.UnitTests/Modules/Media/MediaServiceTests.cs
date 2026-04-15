using FluentAssertions;
using FluentValidation;
using MediatR;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Files.Data.Entities;
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
    private readonly Mock<IFileService> _mockFileService;
    private readonly Mock<IValidator<MediaUpsertRequest>> _mockValidator;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly MediaService _service;
    private const string DefaultUserId = "test-user-1";

    public MediaServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _mockFileService = new Mock<IFileService>();
        _mockFileService.Setup(f => f.GetFileUrl(It.IsAny<string>(), It.IsAny<FileEntityType>()))
            .Returns((string path, FileEntityType type) => path);

        _mockValidator = new Mock<IValidator<MediaUpsertRequest>>();
        _mockValidator.Setup(v => v.Validate(It.IsAny<MediaUpsertRequest>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _mockPublisher = new Mock<IPublisher>();

        _service = new MediaService(_context, _mockFileService.Object, _mockValidator.Object, _mockPublisher.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateMediaAsync_WhenDuplicateExists_ThrowsDomainException()
    {
        // Arrange
        _context.Media.Add(new MediaEntity
        {
            Id = 1,
            Title = "Inception",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2010, 7, 16),
        });
        await _context.SaveChangesAsync();

        var act = () => _service.CreateMediaAsync(DefaultUserId, new MediaUpsertRequest
        {
            Title = "Inception",
            MediaTypeId = -3,
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
            MediaTypeId = -3,
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
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "media_validation_error");
    }

    [Fact]
    public async Task CreateMediaAsync_WithCoverUploadId_AssignsCoverId()
    {
        // Arrange
        var cover = new MediaCover { Id = 100, FileUploadId = 123, FileKey = "covers/interstellar.png", FileName = "interstellar.png", FileContentType = "image/png", FileSizeBytes = 1024 };
        _context.MediaCovers.Add(cover);
        await _context.SaveChangesAsync();

        var request = new MediaUpsertRequest
        {
            Title = "Interstellar",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2014, 11, 7),
            CoverUploadId = 123
        };

        // Act
        var result = await _service.CreateMediaAsync(DefaultUserId, request);

        // Assert
        result.Should().NotBeNull();
        var entity = await _context.Media.FirstAsync(m => m.Id == result.Id);
        entity.CoverId.Should().Be(100);
    }

    [Fact]
    public async Task UpdateMediaAsync_WithCoverUploadId_UpdatesCoverId()
    {
        // Arrange
        var oldCover = new MediaCover { Id = 100, FileUploadId = 123, FileKey = "covers/old.png", FileName = "old.png", FileContentType = "image/png", FileSizeBytes = 1024 };
        var newCover = new MediaCover { Id = 200, FileUploadId = 456, FileKey = "covers/new.png", FileName = "new.png", FileContentType = "image/png", FileSizeBytes = 2048 };
        _context.MediaCovers.AddRange(oldCover, newCover);

        var existingMedia = new MediaEntity
        {
            Title = "Interstellar",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2014, 11, 7),
            CoverId = 100
        };
        _context.Media.Add(existingMedia);
        await _context.SaveChangesAsync();

        var request = new MediaUpsertRequest
        {
            Id = existingMedia.Id,
            Title = "Interstellar Updated",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2014, 11, 7),
            CoverUploadId = 456
        };

        // Act
        await _service.UpdateMediaAsync(DefaultUserId, existingMedia.Id, request);

        // Assert
        var entity = await _context.Media.FirstAsync(m => m.Id == existingMedia.Id);
        entity.Title.Should().Be("Interstellar Updated");
        entity.CoverId.Should().Be(200);
    }

    [Fact]
    public async Task DeleteMediaAsync_DeletesMedia()
    {
        // Arrange
        var media = new MediaEntity
        {
            Title = "To Delete",
            MediaTypeId = -3,
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
            MediaTypeId = -3,
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
    public async Task CreateMediaAsync_WhenMediaTypeNotFound_ThrowsDomainException()
    {
        // Arrange
        var request = new MediaUpsertRequest
        {
            Title = "New Movie",
            MediaTypeId = 99999,  // Non-existent
            ReleaseDate = new DateOnly(2020, 1, 1),
        };

        // Act
        var act = () => _service.CreateMediaAsync(DefaultUserId, request);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "media_type_not_found");
    }

    [Fact]
    public async Task CreateMediaAsync_WhenCoverNotFound_ThrowsDomainException()
    {
        var request = new MediaUpsertRequest
        {
            Title = "New Movie",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2020, 1, 1),
            CoverUploadId = 99999,  // Non-existent
        };

        // Act
        var act = () => _service.CreateMediaAsync(DefaultUserId, request);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "cover_not_found");
    }
}
