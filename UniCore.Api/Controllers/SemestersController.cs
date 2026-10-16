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
    [Route("api/v1/semesters")]
    [Authorize]
    public sealed class SemestersController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List([FromServices] AcademicSemesterHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? sort = null, [FromQuery] Guid? academicYearId = null, [FromQuery] Guid? academicTermId = null, [FromQuery] SemesterStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), sort, academicYearId, academicTermId, status, cancellationToken)).ToActionResult(HttpContext);

        // The semester of the current academic year that contains today (404 NO_CURRENT_SEMESTER when there is none).
        [HttpGet("current")]
        public async Task<IActionResult> Current([FromServices] IAcademicCalendarService calendar, CancellationToken cancellationToken)
            => (await calendar.GetCurrentSemesterAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{semesterId:guid}")]
        public async Task<IActionResult> Get(Guid semesterId, [FromServices] AcademicSemesterHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(semesterId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSemesterRequest request, [FromServices] AcademicSemesterHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateSemesterCommand(request.AcademicYearId ?? Guid.Empty, request.AcademicTermId ?? Guid.Empty, request.StartDate ?? default, request.EndDate ?? default, request.Windows?.ToWindows());
            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);
            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/semesters/{summary.Id}", summary));
        }

        [HttpPut("{semesterId:guid}")]
        public async Task<IActionResult> Update(Guid semesterId, [FromBody] UpdateSemesterRequest request, [FromServices] AcademicSemesterHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdateSemesterCommand(request.StartDate ?? default, request.EndDate ?? default, request.Windows?.ToWindows());
            return (await handler.UpdateAsync(semesterId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{semesterId:guid}/advance")]
        public async Task<IActionResult> Advance(Guid semesterId, [FromBody] AdvanceSemesterRequest request, [FromServices] AcademicSemesterHandler handler, CancellationToken cancellationToken)
            => (await handler.AdvanceStatusAsync(semesterId, request.Status, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
