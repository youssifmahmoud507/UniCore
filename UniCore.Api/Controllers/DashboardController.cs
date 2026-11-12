using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Extensions;
using UniCore.Api.Security;
using UniCore.Application.Modules.Reporting.Dashboard;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Api.Controllers
{
    // The numbers on the home screen: only the widgets the signed-in user may see.
    [ApiController]
    [Route("api/v1/dashboard")]
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class DashboardController : ControllerBase
    {
        [HttpGet]
        [HasPermission(Permissions.Reports.Read)]
        public async Task<IActionResult> Get([FromServices] DashboardHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(cancellationToken)).ToActionResult(HttpContext);
    }

}
