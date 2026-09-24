using Microsoft.AspNetCore.Mvc;
using MediaRankerServer.Shared.Exceptions;

namespace MediaRankerServer.Modules.Test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpPost("helloWorld")]
        public IActionResult HelloWorld()
        {
            return Ok(new { message = "Hello, World!" });
        }

        [HttpPost("domainError")]
        public IActionResult DomainError()
        {
            throw new DomainException(
                "Simulated domain exception from test endpoint.",
                "test_domain_error"
            );
        }

        [HttpPost("unexpectedError")]
        public IActionResult UnexpectedError()
        {
            throw new InvalidOperationException("Simulated unexpected exception from test endpoint.");
        }

        [HttpPost("triggerImdbImport")]
        public IActionResult TriggerImdbImport()
        {
            return Problem(statusCode: StatusCodes.Status410Gone, title: "Manual IMDb import disabled",
                detail: "Use the finite catalog bootstrap job with a calibrated profile.");
        }
        [HttpPost("triggerImdbLoad")]
        public IActionResult TriggerImdbLoad()
        {
            return Problem(statusCode: StatusCodes.Status410Gone, title: "Manual IMDb loading disabled",
                detail: "The catalog job loads only after all required feeds complete.");
        }
    }
}
