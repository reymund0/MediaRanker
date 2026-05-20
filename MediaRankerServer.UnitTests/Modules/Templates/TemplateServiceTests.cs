using FluentAssertions;
using FluentValidation;
using MediatR;
using Moq;
using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Modules.Templates.Data.Entities;
using MediaRankerServer.Modules.Templates.Events;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.UnitTests.Shared;

namespace MediaRankerServer.UnitTests.Modules.Templates;

public class TemplateServiceTests
{
    private readonly PostgreSQLContext _context;
    private readonly Mock<IValidator<TemplateUpsertRequest>> _mockValidator;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly TemplateService _service;

    public TemplateServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _mockValidator = new Mock<IValidator<TemplateUpsertRequest>>();
        _mockPublisher = new Mock<IPublisher>();

        // Default validator behavior (pass)
        _mockValidator.Setup(v => v.Validate(It.IsAny<TemplateUpsertRequest>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _service = new TemplateService(_context, _mockValidator.Object, _mockPublisher.Object);
    }

    [Fact]
    public async Task UpdateTemplateAsync_SystemTemplate_ThrowsDomainException()
    {
        // Act
        var act = () => _service.UpdateTemplateAsync("system", -1, new TemplateUpsertRequest 
        { 
            Name = "New Name", 
            MediaType = "Movie", 
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
            MediaType = "Movie" 
        };
        _context.Templates.Add(userTemplate);
        await _context.SaveChangesAsync();

        // Act
        var act = () => _service.UpdateTemplateAsync("user-2", 1, new TemplateUpsertRequest 
        { 
            Name = "New Name", 
            MediaType = "Movie", 
            Fields = [] 
        });

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "template_forbidden");
    }

    [Fact]
    public async Task DeleteTemplateAsync_SystemTemplate_ThrowsDomainException()
    {
        // Act
        var act = () => _service.DeleteTemplateAsync("system", -1);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "template_forbidden");
    }

    [Fact]
    public async Task DeleteTemplateAsync_PublishesTemplateDeletedEvent()
    {
        // Arrange
        var template = new Template
        {
            Id = 10,
            Name = "My Template",
            UserId = "user-1",
            MediaType = "Movie"
        };
        _context.Templates.Add(template);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteTemplateAsync("user-1", 10);

        // Assert
        _mockPublisher.Verify(
            p => p.Publish(It.Is<TemplateDeletedEvent>(e => e.TemplateId == 10), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenSystemTemplate_DoesNotPublishEvent()
    {
        // Arrange
        var systemTemplate = new Template
        {
            Id = -2,
            Name = "System",
            UserId = "system",
            MediaType = "Movie"
        };
        _context.Templates.Add(systemTemplate);
        await _context.SaveChangesAsync();

        // Act
        var act = () => _service.DeleteTemplateAsync("system", -2);
        await act.Should().ThrowAsync<DomainException>();

        // Assert
        _mockPublisher.Verify(
            p => p.Publish(It.IsAny<TemplateDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
