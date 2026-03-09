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

namespace MediaRankerServer.UnitTests.Modules.Templates;

public class TemplatesServiceTests
{
    private readonly PostgreSQLContext _context;
    private readonly Mock<IValidator<TemplateUpsertRequest>> _mockValidator;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly TemplatesService _service;

    public TemplatesServiceTests()
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

        _service = new TemplatesService(_context, _mockValidator.Object, _mockPublisher.Object);
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
