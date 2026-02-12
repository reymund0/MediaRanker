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
            return Ok("Hello, World!");
        }
    }
}
