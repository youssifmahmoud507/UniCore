using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Attendance;

namespace UniCore.Api.Controllers
{
    // Percentages and warnings. Attendance.Read: a student reads their own, an instructor or a head reads their sections.
    [ApiController]
    [Route("api/v1/attendance/reports")]
    [Authorize]
    public sealed class AttendanceReportsController : ControllerBase
    {
        [HttpGet("sections/{sectionId:guid}")]
        public async Task<IActionResult> Section(Guid sectionId, [FromServices] AttendanceReportHandler handler, CancellationToken cancellationToken)
            => (await handler.GetSectionAsync(sectionId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("sections/{sectionId:guid}/warnings")]
        public async Task<IActionResult> SectionWarnings(Guid sectionId, [FromServices] AttendanceReportHandler handler, CancellationToken cancellationToken)
            => (await handler.ListSectionWarningsAsync(sectionId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("students/{studentId:guid}/sections/{sectionId:guid}")]
        public async Task<IActionResult> Student(Guid studentId, Guid sectionId, [FromServices] AttendanceReportHandler handler, CancellationToken cancellationToken)
            => (await handler.GetStudentAsync(studentId, sectionId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("students/{studentId:guid}/warnings")]
        public async Task<IActionResult> StudentWarnings(Guid studentId, [FromServices] AttendanceReportHandler handler, CancellationToken cancellationToken, [FromQuery] Guid? sectionId = null)
            => (await handler.ListStudentWarningsAsync(studentId, sectionId, cancellationToken)).ToActionResult(HttpContext);
    }
}
