using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Calendar;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Calendar;
using UniCore.Domain.Modules.Calendar;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/academic-years")]
    [Authorize]
    public sealed class AcademicYearsController : ControllerBase
    {
        // Permissions are checked inside the handler.
        [HttpGet]
        public async Task<IActionResult> List([FromServices] AcademicYearHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] AcademicYearStatus? status = null, [FromQuery] bool? isCurrent = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, status, isCurrent, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{yearId:guid}")]
        public async Task<IActionResult> Get(Guid yearId, [FromServices] AcademicYearHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(yearId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AcademicYearRequest request, [FromServices] AcademicYearHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateAcademicYearCommand(request.Name, request.StartDate ?? default, request.EndDate ?? default);
            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);
            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/academic-years/{summary.Id}", summary));
        }

        [HttpPut("{yearId:guid}")]
        public async Task<IActionResult> Update(Guid yearId, [FromBody] AcademicYearRequest request, [FromServices] AcademicYearHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdateAcademicYearCommand(request.Name, request.StartDate ?? default, request.EndDate ?? default);
            return (await handler.UpdateAsync(yearId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{yearId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid yearId, [FromServices] AcademicYearHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(yearId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{yearId:guid}/make-current")]
        public async Task<IActionResult> MakeCurrent(Guid yearId, [FromServices] AcademicYearHandler handler, CancellationToken cancellationToken)
            => (await handler.MakeCurrentAsync(yearId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{yearId:guid}/close")]
        public async Task<IActionResult> Close(Guid yearId, [FromServices] AcademicYearHandler handler, CancellationToken cancellationToken)
            => (await handler.CloseAsync(yearId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
