using FluentAssertions;
using MediaRankerServer.Modules.Test.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ImdbManualTriggerTests
{
    [Fact]
    public void ImportCannotBypassTheCalibratedJobProfile()
    {
        var result = new TestController().TriggerImdbImport();
        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(410);
        response.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(410);
    }

    [Fact]
    public void LoadCannotBypassTheCompleteFeedJobGate()
    {
        var result = new TestController().TriggerImdbLoad();
        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(410);
        response.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(410);
    }
}
