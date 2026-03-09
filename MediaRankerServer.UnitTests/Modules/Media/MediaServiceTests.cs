using FluentAssertions;
using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class MediaServiceTests
{
    private readonly PostgreSQLContext _context;
    private readonly Mock<IValidator<MediaUpsertRequest>> _mockValidator;
    private readonly MediaService _service;

    public MediaServiceTests()
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PostgreSQLContext(options);
        _mockValidator = new Mock<IValidator<MediaUpsertRequest>>();

        _mockValidator.Setup(v => v.Validate(It.IsAny<MediaUpsertRequest>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _service = new MediaService(_context, _mockValidator.Object);
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

        var act = () => _service.CreateMediaAsync(new MediaUpsertRequest
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
        var act = () => _service.UpdateMediaAsync(999, new MediaUpsertRequest
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

        var act = () => _service.CreateMediaAsync(new MediaUpsertRequest
        {
            Title = "",
            MediaTypeId = -3,
            ReleaseDate = new DateOnly(2020, 1, 1),
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "media_validation_error");
    }
}
