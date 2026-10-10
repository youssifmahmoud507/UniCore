using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniCore.Api.Contracts.Auth;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Identity;

namespace UniCore.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class AuthController : ControllerBase
    {
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, [FromServices] LoginHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.HandleAsync(new LoginCommand(request.Login, request.Password, ClientIp(), UserAgent()), cancellationToken);

            return result.ToActionResult(HttpContext);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Refresh)]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, [FromServices] RefreshHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.HandleAsync(new RefreshCommand(request.RefreshToken, ClientIp(), UserAgent()), cancellationToken);

            return result.ToActionResult(HttpContext);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request, [FromServices] LogoutHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.HandleAsync(new LogoutCommand(request.RefreshToken, ClientIp(), UserAgent()), cancellationToken);
            return result.ToActionResult(HttpContext);
        }

        private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? UserAgent() => Request.Headers.UserAgent.ToString();
    }
}
