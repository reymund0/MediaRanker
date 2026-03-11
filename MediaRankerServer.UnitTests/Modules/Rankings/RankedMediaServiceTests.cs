using MediaRankerServer.Modules.Rankings.Services;
using MediaRankerServer.Modules.Rankings.Contracts;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Shared.Data;
using Moq;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Modules.Rankings.Entities;

namespace MediaRankerServer.UnitTests.Modules.Rankings;

public class RankedMediaServiceTests
{
    private readonly RankedMediaService _service;
    private readonly PostgreSQLContext _dbContext;
    private readonly Mock<IValidator<RankedMediaUpsertRequest>> _mockValidator;
    private readonly Mock<IMediaService> _mockMediaService;
    private readonly MediaDto _testMedia;
    private readonly MediaTypeDto _testMediaType;
    private readonly Mock<ITemplatesService> _mockTemplatesService;
    private readonly TemplateDto _testTemplate;
    private readonly List<TemplateFieldDto> _testTemplateFields;
    private RankedMediaUpsertRequest _testRequest;

    public RankedMediaServiceTests()
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new PostgreSQLContext(options);

        _mockValidator = new Mock<IValidator<RankedMediaUpsertRequest>>();
        _mockValidator.Setup(v => v.Validate(It.IsAny<RankedMediaUpsertRequest>()))
            .Returns(new ValidationResult());

        _testMediaType = new MediaTypeDto { Id = 1, Name = "Book" };
        _testMedia = new MediaDto { Id = 1, MediaType = _testMediaType };
        _mockMediaService = new Mock<IMediaService>();
        _mockMediaService.Setup(m => m.GetMediaByIdAsync(_testMedia.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testMedia);

        _testTemplateFields = [new TemplateFieldDto { Id = 1, Name = "Rating" }, new TemplateFieldDto { Id = 2, Name = "Review" }];
        _testTemplate = new TemplateDto {Id = 1, MediaType = _testMediaType, Fields = _testTemplateFields};
        _mockTemplatesService = new Mock<ITemplatesService>();
        _mockTemplatesService.Setup(t => t.GetTemplateByIdAsync(_testTemplate.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testTemplate);


        _testRequest = new RankedMediaUpsertRequest
        {
            MediaId = _testMedia.Id,
            TemplateId = _testTemplate.Id,
            Scores = [new RankedMediaScoreUpsertRequest { TemplateFieldId = 1, Value = 5 }]
        };
        
        _service = new RankedMediaService(_dbContext, _mockValidator.Object, _mockMediaService.Object, _mockTemplatesService.Object);
    }
    
    [Fact]
    public async Task ValidateRankedMediaUpsertRequest_WhenMediaIdIsInvalid_ThrowsValidationDomainException()
    {
        // Arrange
        _testRequest.MediaId = -10;
        
        // Act & Assert
        var act = () => _service.CreateRankedMediaAsync("test-user", _testRequest, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "ranked_media_upsert_validation_error")
            .Where(e => e.Message.Contains("MediaId"));
    }

    [Fact]
    public async Task ValidateRankedMediaUpsertRequest_WhenTemplateIdIsInvalid_ThrowsValidationDomainException()
    {
        // Arrange
        _testRequest.TemplateId = -10;
        
        // Act & Assert
        var act = () => _service.CreateRankedMediaAsync("test-user", _testRequest, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "ranked_media_upsert_validation_error")
            .Where(e => e.Message.Contains("TemplateId"));
    }

    [Fact]
    public async Task ValidateRankedMediaUpsertRequest_WhenTemplateFieldsAreInvalid_ThrowsValidationDomainException()
    {
        // Arrange
        _testRequest.Scores = [new RankedMediaScoreUpsertRequest { TemplateFieldId = 999, Value = 5 }];
        
        // Act & Assert
        var act = () => _service.CreateRankedMediaAsync("test-user", _testRequest, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "ranked_media_upsert_validation_error")
            .Where(e => e.Message.Contains("Template field"));
    }

    [Fact]
    public async Task ValidateRankedMediaUpsertRequest_WhenMediaTypeIsIncompatible_ThrowsValidationDomainException()
    {
        // Arrange
        _testMedia.MediaType = new MediaTypeDto { Id = 2, Name = "Movie" };
        _mockMediaService.Setup(m => m.GetMediaByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testMedia);
        
        // Act & Assert
        var act = () => _service.CreateRankedMediaAsync("test-user", _testRequest, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "ranked_media_upsert_validation_error")
            .Where(e => e.Message.Contains("Media type"));
    }


    [Fact]
    public async Task ValidateUpdateRankedMediaAsync_WhenRankedMediaNotFound_ThrowsDomainException()
    {
        // Arrange
        var rankedMediaId = 999;
        
        // Act & Assert
        var act = () => _service.UpdateRankedMediaAsync("test-user", rankedMediaId, _testRequest, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "ranked_media_not_found");
    }

    [Fact]
    public async Task ValidateUpdateRankedMediaAsync_WhenRankedMediaDoesNotBelongToUser_ThrowsDomainException()
    {
        // Arrange
        var rankedMedia = new RankedMedia
        {
            UserId = "other-user"
        };
        _dbContext.RankedMedia.Add(rankedMedia);
        await _dbContext.SaveChangesAsync();
        
        // Act & Assert
        var act = () => _service.UpdateRankedMediaAsync("test-user", rankedMedia.Id, _testRequest, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "ranked_media_forbidden");
    }

    [Fact]
    public async Task ValidateDeleteRankedMediaAsync_WhenRankedMediaNotFound_ThrowsDomainException()
    {
        // Arrange
        var rankedMediaId = 999;
        
        // Act & Assert
        var act = () => _service.DeleteRankedMediaAsync("test-user", rankedMediaId, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "ranked_media_not_found");
    }

    [Fact]
    public async Task ValidateDeleteRankedMediaAsync_WhenRankedMediaDoesNotBelongToUser_ThrowsDomainException()
    {
        // Arrange
        var rankedMedia = new RankedMedia
        {
            UserId = "other-user"
        };
        _dbContext.RankedMedia.Add(rankedMedia);
        await _dbContext.SaveChangesAsync();
        
        // Act & Assert
        var act = () => _service.DeleteRankedMediaAsync("test-user", rankedMedia.Id, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "ranked_media_forbidden");
    }
}
