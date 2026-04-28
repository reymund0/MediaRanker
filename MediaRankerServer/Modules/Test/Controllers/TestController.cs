using Microsoft.AspNetCore.Mvc;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController(ImdbImportService importService, ImdbLoadService loadService) : ControllerBase
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
        public async Task<IActionResult> TriggerImdbImport(CancellationToken cancellationToken)
        {
            var result = await importService.ImportAsync(cancellationToken);
            
            return Ok(new { message = "IMDB import completed.", result });
        }
        [HttpPost("triggerImdbLoad")]
        public async Task<IActionResult> TriggerImdbLoad(CancellationToken cancellationToken)
        {
            var result = await loadService.LoadAsync(cancellationToken);
            
            return Ok(new { message = "IMDB load completed.", result });
        }
    }
}
