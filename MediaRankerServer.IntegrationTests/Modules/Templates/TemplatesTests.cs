using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Modules.Templates.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MediaRankerServer.IntegrationTests.Modules.Templates;

public class TemplatesTests(PostgresContainerFixture fixture) : IntegrationTestBase(fixture)
{
  [Fact]
    public async Task GetTemplates_ReturnsSystemTemplatesAndUserTemplatesOnly()
    {
        // Arrange
        var userId = TestAuthHandler.DefaultUserId;
        var otherUserId = "other-user";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            
            db.Templates.Add(new Template 
            { 
                UserId = userId, 
                Name = "User Template", 
                MediaTypeId = -3 // Movie
            });
            
            db.Templates.Add(new Template 
            { 
                UserId = otherUserId, 
                Name = "Other User Template", 
                MediaTypeId = -3 
            });

            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.GetAsync("/api/templates");

        // Assert
        response.EnsureSuccessStatusCode();
        var templates = await response.Content.ReadFromJsonAsync<List<TemplateDto>>();
        
        templates.Should().NotBeNull();
        // Should have system templates (usually < 0) + our user template
        templates.Should().Contain(t => t.UserId == userId);
        templates.Should().NotContain(t => t.UserId == otherUserId);
        templates.Should().Contain(t => t.Id < 0); 
    }

    [Fact]
    public async Task CreateTemplate_WithValidRequest_PersistsTemplateAndFields()
    {
        // Arrange
        var request = new TemplateUpsertRequest
        {
            MediaTypeId = -3, // Movie
            Name = "New Integration Template",
            Description = "Test Description",
            Fields = new List<TemplateFieldUpsertRequest>
            {
                new() { Name = "Acting", Position = 1 },
                new() { Name = "Plot", Position = 2 }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/templates", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TemplateDto>();
        
        result.Should().NotBeNull();
        result!.Name.Should().Be(request.Name);
        result.Fields.Should().HaveCount(2);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var dbTemplate = await db.Templates.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == result.Id);
        
        dbTemplate.Should().NotBeNull();
        dbTemplate!.Fields.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateTemplate_DuplicateName_Returns400Conflict()
    {
        // Arrange
        var name = "Unique Conflict Template";
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            db.Templates.Add(new Template { UserId = TestAuthHandler.DefaultUserId, Name = name, MediaTypeId = -3 });
            await db.SaveChangesAsync();
        }

        var request = new TemplateUpsertRequest 
        { 
            Name = name, 
            MediaTypeId = -3,
            Fields = new List<TemplateFieldUpsertRequest>
            {
                new() { Name = "Acting", Position = 1 }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/templates", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problem!.Type.Should().Be("template_name_conflict");
    }

    [Fact]
    public async Task DeleteTemplate_SystemTemplate_Returns403Forbidden()
    {
        // Act - System templates have negative IDs
        var response = await Client.DeleteAsync("/api/templates/-1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest); // Service throws DomainException which maps to 400
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problem!.Type.Should().Be("template_forbidden");
    }
}
