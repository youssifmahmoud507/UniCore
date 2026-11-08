using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Attendance;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Attendance;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/attendance/sessions")]
    [Authorize]
    public sealed class AttendanceSessionsController : ControllerBase
    {
        // Permissions are checked inside the handlers: Attendance.Record on the section to open, mark and close; Attendance.Read to read.

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] Guid sectionId, [FromServices] AttendanceSessionHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] AttendanceSessionStatus? status = null)
            => (await handler.ListAsync(sectionId, status, new PaginationQuery(page, pageSize), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{sessionId:guid}")]
        public async Task<IActionResult> Get(Guid sessionId, [FromServices] AttendanceSessionHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(sessionId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Open([FromBody] OpenSessionRequest request, [FromServices] AttendanceSessionHandler handler, CancellationToken cancellationToken)
        {
            if (request.SessionDate is not { } date || request.StartTime is not { } start)
            {
                return Result<SessionSummary>.Fail(AttendanceErrors.SessionInvalid).ToActionResult(HttpContext);
            }

            var result = await handler.OpenAsync(new OpenSessionCommand(request.CourseSectionId ?? Guid.Empty, date, start), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/attendance/sessions/{summary.Id}", summary));
        }

        [HttpPost("{sessionId:guid}/marks")]
        public async Task<IActionResult> Mark(Guid sessionId, [FromBody] MarkAttendanceRequest request, [FromServices] AttendanceSessionHandler handler, CancellationToken cancellationToken)
        {
            if (request.Marks is null || request.Marks.Any(m => m.StudentId is null || m.Status is null))
            {
                return Result<SessionDetail>.Fail(AttendanceErrors.RecordInvalid).ToActionResult(HttpContext);
            }

            var marks = request.Marks.Select(m => new StudentMark(m.StudentId!.Value, m.Status!.Value, m.MinutesLate)).ToList();

            return (await handler.MarkAsync(sessionId, new MarkAttendanceCommand(marks), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{sessionId:guid}/close")]
        public async Task<IActionResult> Close(Guid sessionId, [FromServices] AttendanceSessionHandler handler, CancellationToken cancellationToken)
            => (await handler.CloseAsync(sessionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
