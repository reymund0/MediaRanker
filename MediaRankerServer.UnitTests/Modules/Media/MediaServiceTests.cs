using FluentAssertions;
using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class MediaServiceTests
{
    private readonly PostgreSQLContext _context;
    private readonly Mock<IMediaCoverService> _mockCoverService;
    private readonly Mock<IValidator<MediaUpsertRequest>> _mockValidator;
    private readonly MediaService _service;
    private const string DefaultUserId = "test-user-1";

    public MediaServiceTests()
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PostgreSQLContext(options);
        _mockCoverService = new Mock<IMediaCoverService>();
        _mockValidator = new Mock<IValidator<MediaUpsertRequest>>();

        _mockValidator.Setup(v => v.Validate(It.IsAny<MediaUpsertRequest>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _service = new MediaService(_context, _mockCoverService.Object, _mockValidator.Object);
    }

    [Fact]
    public async Task CreateMediaAsync_WhenDuplicateExists_ThrowsDomainException()
    {
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
    public async Task CreateMediaAsync_WithCoverUploadId_CopiesMetadataToEntity()
    {
        // Arrange
        var request = new MediaUpsertRequest
        {
            Title = "Interstellar",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2014, 11, 7),
            CoverUploadId = 123
        };

        var fileDto = new MediaRankerServer.Modules.Files.Contracts.FileDto
        {
            UploadId = 123,
            FileKey = "covers/interstellar.png",
            FileName = "interstellar.png",
            ContentType = "image/png",
            FileSizeBytes = 1024
        };

        _mockCoverService.Setup(s => s.CopyCoverFileAsync(DefaultUserId, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDto);

        // Seed MediaType so GetMediaByIdAsync (called via CreateMediaAsync) works
        _context.MediaTypes.Add(new MediaType { Id = -3, Name = "Movie" });
        await _context.SaveChangesAsync();

        _mockCoverService.Setup(s => s.GetCoverUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://fake-url");

        // Act
        var result = await _service.CreateMediaAsync(DefaultUserId, request);

        // Assert
        result.Should().NotBeNull();
        _mockCoverService.Verify(s => s.CopyCoverFileAsync(DefaultUserId, 123, It.IsAny<CancellationToken>()), Times.Once);

        var entity = await _context.Media.FirstAsync(m => m.Id == result.Id);
        entity.CoverFileUploadId.Should().Be(123);
        entity.CoverFileKey.Should().Be("covers/interstellar.png");
        entity.CoverFileName.Should().Be("interstellar.png");
        entity.CoverFileContentType.Should().Be("image/png");
        entity.CoverFileSizeBytes.Should().Be(1024);
    }

    [Fact]
    public async Task UpdateMediaAsync_WithCoverUploadId_UpdatesMetadataOnEntity()
    {
        // Arrange
        var existingMedia = new MediaEntity
        {
            Title = "Interstellar",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2014, 11, 7)
        };
        _context.Media.Add(existingMedia);
        // Seed MediaType so GetMediaByIdAsync works
        _context.MediaTypes.Add(new MediaType { Id = -3, Name = "Movie" });
        await _context.SaveChangesAsync();

        var request = new MediaUpsertRequest
        {
            Id = existingMedia.Id,
            Title = "Interstellar Updated",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2014, 11, 7),
            CoverUploadId = 456
        };

        var fileDto = new MediaRankerServer.Modules.Files.Contracts.FileDto
        {
            UploadId = 456,
            FileKey = "covers/interstellar_new.png",
            FileName = "interstellar_new.png",
            ContentType = "image/png",
            FileSizeBytes = 2048
        };

        _mockCoverService.Setup(s => s.CopyCoverFileAsync(DefaultUserId, 456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDto);

        _mockCoverService.Setup(s => s.GetCoverUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://fake-url");

        // Act
        await _service.UpdateMediaAsync(DefaultUserId, existingMedia.Id, request);

        // Assert
        _mockCoverService.Verify(s => s.CopyCoverFileAsync(DefaultUserId, 456, It.IsAny<CancellationToken>()), Times.Once);

        var entity = await _context.Media.FirstAsync(m => m.Id == existingMedia.Id);
        entity.Title.Should().Be("Interstellar Updated");
        entity.CoverFileUploadId.Should().Be(456);
        entity.CoverFileKey.Should().Be("covers/interstellar_new.png");
    }

    [Fact]
    public async Task DeleteMediaAsync_WithCover_CallsDeleteCoverFile()
    {
        // Arrange
        var media = new MediaEntity
        {
            Title = "To Delete",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2020, 1, 1),
            CoverFileUploadId = 789
        };
        _context.Media.Add(media);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteMediaAsync(media.Id);

        // Assert
        _mockCoverService.Verify(s => s.DeleteCoverFileAsync(789, It.IsAny<CancellationToken>()), Times.Once);
        _context.Media.Should().NotContain(m => m.Id == media.Id);
    }
}
