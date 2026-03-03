using MediaRankerServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Controllers
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
    }
}
