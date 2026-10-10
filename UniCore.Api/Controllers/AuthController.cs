using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniCore.Api.Contracts.Auth;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Identity;

namespace UniCore.Api.Controllers
{
    [Route("api/v1/auth")]
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


        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.ForgotPassword)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, [FromServices] ForgotPasswordHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.HandleAsync(
                new ForgotPasswordCommand(request.Email, ClientIp(), UserAgent()), cancellationToken);

            return result.ToActionResult(HttpContext, () => Ok(new MessageResponse(
                "If an account exists for this email, a verification code has been sent.")));
        }

        [HttpPost("verify-otp")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.VerifyOtp)]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, [FromServices] VerifyOtpHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.HandleAsync(
                new VerifyOtpCommand(request.Email, request.Otp, ClientIp(), UserAgent()), cancellationToken);

            return result.ToActionResult(HttpContext);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.ResetPassword)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, [FromServices] ResetPasswordHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.HandleAsync(
                new ResetPasswordCommand(
                    request.Email, request.ResetToken, request.NewPassword, request.ConfirmPassword,
                    ClientIp(), UserAgent()),
                cancellationToken);

            return result.ToActionResult(HttpContext, () => Ok(new MessageResponse(
                "Your password has been changed. Please sign in again.")));
        }




        private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? UserAgent() => Request.Headers.UserAgent.ToString();
    }
}
