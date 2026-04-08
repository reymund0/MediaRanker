using FluentAssertions;
using FluentValidation;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class MediaCollectionServiceTests
{
    private readonly PostgreSQLContext _context;
    private readonly Mock<IMediaCoverService> _mockCoverService;
    private readonly Mock<IFileService> _mockFileService;
    private readonly Mock<IValidator<MediaCollectionUpsertRequest>> _mockValidator;
    private readonly MediaCollectionService _service;
    private const string DefaultUserId = "test-user-1";

    // Seeded system IDs matching production seeds
    private const long TvShowTypeId = -4;
    private const long MovieTypeId = -3;

    public MediaCollectionServiceTests()
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PostgreSQLContext(options);

        _context.MediaTypes.AddRange(
            new MediaType { Id = TvShowTypeId, Name = "TV Show" },
            new MediaType { Id = MovieTypeId, Name = "Movie" }
        );
        _context.SaveChanges();

        _mockCoverService = new Mock<IMediaCoverService>();
        _mockFileService = new Mock<IFileService>();
        _mockFileService.Setup(f => f.GetFileUrl(It.IsAny<string>(), It.IsAny<FileEntityType>()))
            .Returns((string path, FileEntityType _) => path);

        _mockValidator = new Mock<IValidator<MediaCollectionUpsertRequest>>();
        _mockValidator.Setup(v => v.Validate(It.IsAny<MediaCollectionUpsertRequest>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _service = new MediaCollectionService(
            _context,
            _mockCoverService.Object,
            _mockFileService.Object,
            _mockValidator.Object
        );
    }

    // --- Validator error propagation ---

    [Fact]
    public async Task CreateCollectionAsync_WhenValidationFails_ThrowsValidationDomainException()
    {
        _mockValidator.Setup(v => v.Validate(It.IsAny<MediaCollectionUpsertRequest>()))
            .Returns(new FluentValidation.Results.ValidationResult([
                new FluentValidation.Results.ValidationFailure("Title", "Collection title is required."),
            ]));

        var act = () => _service.CreateCollectionAsync(DefaultUserId, new MediaCollectionUpsertRequest
        {
            Title = "",
            CollectionType = CollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "collection_validation_error");
    }

    // --- Parent validation ---

    [Fact]
    public async Task CreateCollectionAsync_WhenParentNotFound_ThrowsDomainException()
    {
        var act = () => _service.CreateCollectionAsync(DefaultUserId, new MediaCollectionUpsertRequest
        {
            Title = "Season 1",
            CollectionType = CollectionType.Season,
            MediaTypeId = TvShowTypeId,
            ParentMediaCollectionId = 999,
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "collection_parent_not_found");
    }

    [Fact]
    public async Task CreateCollectionAsync_WhenParentMediaTypeMismatches_ThrowsDomainException()
    {
        var series = new MediaCollection
        {
            Title = "Movie Series",
            CollectionType = CollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2020, 1, 1),
        };
        _context.MediaCollections.Add(series);
        await _context.SaveChangesAsync();

        var act = () => _service.CreateCollectionAsync(DefaultUserId, new MediaCollectionUpsertRequest
        {
            Title = "Season 1",
            CollectionType = CollectionType.Season,
            MediaTypeId = TvShowTypeId,
            ParentMediaCollectionId = series.Id,
            ReleaseDate = new DateOnly(2021, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "collection_parent_media_type_mismatch");
    }

    // --- TV Show collection type rules ---

    [Fact]
    public async Task CreateCollectionAsync_TvShow_SeasonWithoutParent_ThrowsDomainException()
    {
        var act = () => _service.CreateCollectionAsync(DefaultUserId, new MediaCollectionUpsertRequest
        {
            Title = "Orphan Season",
            CollectionType = CollectionType.Season,
            MediaTypeId = TvShowTypeId,
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "collection_season_requires_series");
    }

    [Fact]
    public async Task CreateCollectionAsync_TvShow_SeriesWithParent_ThrowsDomainException()
    {
        var parentSeries = new MediaCollection
        {
            Title = "Parent Series",
            CollectionType = CollectionType.Series,
            MediaTypeId = TvShowTypeId,
            ReleaseDate = new DateOnly(2019, 1, 1),
        };
        _context.MediaCollections.Add(parentSeries);
        await _context.SaveChangesAsync();

        var act = () => _service.CreateCollectionAsync(DefaultUserId, new MediaCollectionUpsertRequest
        {
            Title = "Nested Series",
            CollectionType = CollectionType.Series,
            MediaTypeId = TvShowTypeId,
            ParentMediaCollectionId = parentSeries.Id,
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "collection_series_cannot_have_parent");
    }

    [Fact]
    public async Task CreateCollectionAsync_TvShow_SeasonParentIsNotSeries_ThrowsDomainException()
    {
        // Create a Season as the parent (invalid — parent must be a Series)
        var seasonParent = new MediaCollection
        {
            Title = "Some Season",
            CollectionType = CollectionType.Season,
            MediaTypeId = TvShowTypeId,
            ReleaseDate = new DateOnly(2019, 1, 1),
        };
        _context.MediaCollections.Add(seasonParent);
        await _context.SaveChangesAsync();

        var act = () => _service.CreateCollectionAsync(DefaultUserId, new MediaCollectionUpsertRequest
        {
            Title = "Season 1",
            CollectionType = CollectionType.Season,
            MediaTypeId = TvShowTypeId,
            ParentMediaCollectionId = seasonParent.Id,
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "collection_season_requires_series");
    }

    [Fact]
    public async Task CreateCollectionAsync_NonTvShow_SeasonType_ThrowsDomainException()
    {
        var act = () => _service.CreateCollectionAsync(DefaultUserId, new MediaCollectionUpsertRequest
        {
            Title = "Movie Season",
            CollectionType = CollectionType.Season,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "collection_type_unsupported");
    }

    // --- Update & Delete collection not found exceptions ---

    [Fact]
    public async Task UpdateCollectionAsync_WhenNotFound_ThrowsDomainException()
    {
        var act = () => _service.UpdateCollectionAsync(DefaultUserId, 999, new MediaCollectionUpsertRequest
        {
            Title = "Updated",
            CollectionType = CollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "collection_not_found");
    }

    [Fact]
    public async Task DeleteCollectionAsync_WhenNotFound_ThrowsDomainException()
    {
        var act = () => _service.DeleteCollectionAsync(999);

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "collection_not_found");
    }

    // --- Cover file handling ---

    [Fact]
    public async Task CreateCollectionAsync_WithCoverUploadId_CopiesMetadataToEntity()
    {
        var fileDto = new FileDto
        {
            UploadId = 42,
            FileKey = "covers/my-series.png",
            FileName = "my-series.png",
            ContentType = "image/png",
            FileSizeBytes = 2048
        };

        _mockCoverService
            .Setup(s => s.CopyCoverFileAsync(DefaultUserId, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDto);

        var result = await _service.CreateCollectionAsync(DefaultUserId, new MediaCollectionUpsertRequest
        {
            Title = "My Movie Series",
            CollectionType = CollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2020, 1, 1),
            CoverUploadId = 42,
        });

        _mockCoverService.Verify(s => s.CopyCoverFileAsync(DefaultUserId, 42, It.IsAny<CancellationToken>()), Times.Once);

        var entity = await _context.MediaCollections.FirstAsync(mc => mc.Id == result.Id);
        entity.CoverFileUploadId.Should().Be(42);
        entity.CoverFileKey.Should().Be("covers/my-series.png");
        entity.CoverFileName.Should().Be("my-series.png");
        entity.CoverFileContentType.Should().Be("image/png");
        entity.CoverFileSizeBytes.Should().Be(2048);
    }

    [Fact]
    public async Task UpdateCollectionAsync_WithNewCoverUploadId_DeletesOldAndCopiesNew()
    {
        var existing = new MediaCollection
        {
            Title = "Old Series",
            CollectionType = CollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2019, 1, 1),
            CoverFileUploadId = 10,
            CoverFileKey = "covers/old.png",
        };
        _context.MediaCollections.Add(existing);
        await _context.SaveChangesAsync();

        var newFileDto = new FileDto
        {
            UploadId = 20,
            FileKey = "covers/new.png",
            FileName = "new.png",
            ContentType = "image/png",
            FileSizeBytes = 512
        };
        _mockCoverService
            .Setup(s => s.CopyCoverFileAsync(DefaultUserId, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFileDto);

        await _service.UpdateCollectionAsync(DefaultUserId, existing.Id, new MediaCollectionUpsertRequest
        {
            Title = "Updated Series",
            CollectionType = CollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2019, 1, 1),
            CoverUploadId = 20,
        });

        _mockCoverService.Verify(s => s.DeleteCoverFileAsync("covers/old.png", It.IsAny<CancellationToken>()), Times.Once);
        _mockCoverService.Verify(s => s.CopyCoverFileAsync(DefaultUserId, 20, It.IsAny<CancellationToken>()), Times.Once);

        var entity = await _context.MediaCollections.FirstAsync(mc => mc.Id == existing.Id);
        entity.CoverFileKey.Should().Be("covers/new.png");
    }

    [Fact]
    public async Task DeleteCollectionAsync_WithCover_CallsDeleteCoverFile()
    {
        var collection = new MediaCollection
        {
            Title = "To Delete",
            CollectionType = CollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2020, 1, 1),
            CoverFileKey = "covers/to-delete.png",
        };
        _context.MediaCollections.Add(collection);
        await _context.SaveChangesAsync();

        await _service.DeleteCollectionAsync(collection.Id);

        _mockCoverService.Verify(s => s.DeleteCoverFileAsync("covers/to-delete.png", It.IsAny<CancellationToken>()), Times.Once);
        _context.MediaCollections.Should().NotContain(mc => mc.Id == collection.Id);
    }
}
