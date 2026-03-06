using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MediaRankerServer.IntegrationTests.Shared;

public class ErrorPipelineEndpointTests : IntegrationTestBase
{
    public ErrorPipelineEndpointTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetDomainError_Returns400ProblemDetails()
    {
        // Act
        var response = await Client.PostAsync("/api/test/domainError", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Domain error");
        problem.Type.Should().Be("test_domain_error");
    }

    [Fact]
    public async Task GetUnexpectedError_Returns500ProblemDetailsWithErrorId()
    {
        // Act
        var response = await Client.PostAsync("/api/test/unexpectedError", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        
        problem.Should().NotBeNull();
        problem!.Type.Should().Be("unexpected_error");
        problem.Extensions.Should().ContainKey("errorId");
        problem.Extensions["errorId"]?.ToString().Should().NotBeNullOrEmpty();
    }
}
