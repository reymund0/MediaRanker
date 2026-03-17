using FluentAssertions;
using FluentValidation;
using MediatR;
using Moq;
using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Modules.Templates.Entities;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Contracts;

namespace MediaRankerServer.UnitTests.Modules.Templates;

public class TemplateServiceTests
{
    private readonly PostgreSQLContext _context;
    private readonly Mock<IValidator<TemplateUpsertRequest>> _mockValidator;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly TemplateService _service;
    private readonly Mock<IMediaService> _mediaService;

    public TemplateServiceTests()
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PostgreSQLContext(options);
        _mockValidator = new Mock<IValidator<TemplateUpsertRequest>>();
        _mockPublisher = new Mock<IPublisher>();

        // Default validator behavior (pass)
        _mockValidator.Setup(v => v.Validate(It.IsAny<TemplateUpsertRequest>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _mediaService = new Mock<IMediaService>();
        _mediaService.Setup(m => m.GetMediaTypeByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((long id, CancellationToken _) => new MediaTypeDto { Id = id, Name = "Test" });

        _service = new TemplateService(_context, _mockValidator.Object, _mockPublisher.Object, _mediaService.Object);
    }

    [Fact]
    public async Task UpdateTemplateAsync_SystemTemplate_ThrowsDomainException()
    {
        // Arrange
        var systemTemplate = new Template 
        { 
            Id = -1, 
            Name = "System", 
            UserId = "system", 
            MediaTypeId = -1 
        };
        _context.Templates.Add(systemTemplate);
        await _context.SaveChangesAsync();

        // Act
        var act = () => _service.UpdateTemplateAsync("system", -1, new TemplateUpsertRequest 
        { 
            Name = "New Name", 
            MediaTypeId = -1, 
            Fields = [] 
        });

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "template_forbidden");
    }

    [Fact]
    public async Task UpdateTemplateAsync_WrongUser_ThrowsDomainException()
    {
        // Arrange
        var userTemplate = new Template 
        { 
            Id = 1, 
            Name = "User Template", 
            UserId = "user-1", 
            MediaTypeId = -1 
        };
        _context.Templates.Add(userTemplate);
        await _context.SaveChangesAsync();

        // Act
        var act = () => _service.UpdateTemplateAsync("user-2", 1, new TemplateUpsertRequest 
        { 
            Name = "New Name", 
            MediaTypeId = -1, 
            Fields = [] 
        });

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "template_forbidden");
    }

    [Fact]
    public async Task UpdateTemplateAsync_InvalidMediaType_ThrowsDomainException()
    {
        // Arrange
        var request = new TemplateUpsertRequest 
        { 
            Name = "New Template", 
            MediaTypeId = 999, 
            Fields = [] 
        };

        _mediaService.Setup(m => m.GetMediaTypeByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((MediaTypeDto?)null);

        // Act
        var act = () => _service.UpdateTemplateAsync("user-1", 1, request);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "template_validation_error");
    }

    [Fact]
    public async Task DeleteTemplateAsync_SystemTemplate_ThrowsDomainException()
    {
        // Arrange
        var systemTemplate = new Template 
        { 
            Id = -1, 
            Name = "System", 
            UserId = "system", 
            MediaTypeId = -1 
        };
        _context.Templates.Add(systemTemplate);
        await _context.SaveChangesAsync();

        // Act
        var act = () => _service.DeleteTemplateAsync("system", -1);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "template_forbidden");
    }
}
