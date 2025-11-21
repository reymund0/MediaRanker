using MediaRankerServer.Data.Entities;
using MediaRankerServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController(IUserService userService) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<ActionResult<User>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var user = await userService.Login(request.Username, request.Password, cancellationToken);

            if (user == null)
                return Unauthorized(new { message = "Invalid username or password" });

            return Ok(user);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
