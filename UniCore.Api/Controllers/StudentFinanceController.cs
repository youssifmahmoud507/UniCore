using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Finance;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/students/{studentId:guid}/finance")]
    [Authorize]
    public sealed class StudentFinanceController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(Guid studentId, [FromServices] StudentFinanceHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(studentId, cancellationToken)).ToActionResult(HttpContext);
    }



}
