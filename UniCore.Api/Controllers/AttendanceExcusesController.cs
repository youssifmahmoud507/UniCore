using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniCore.Api.Contracts.Attendance;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Attendance;
using UniCore.Application.Modules.Documents;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/attendance/excuses")]
    [Authorize]
    public sealed class AttendanceExcusesController : ControllerBase
    {
        // A little above the 10 MB file limit, for the form itself. The real limit is checked by the Documents module.
        private const long MaxFormBytes = 12_582_912;

        // The student submits for their own absence. Attendance.SubmitExcuse (checked in the handler).
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFormBytes)]
        [RequestSizeLimit(MaxFormBytes)]
        [EnableRateLimiting(RateLimitPolicies.Upload)]
        public async Task<IActionResult> Submit([FromForm] SubmitExcuseForm form, [FromServices] AttendanceExcuseHandler handler, CancellationToken cancellationToken)
        {
            if (form.AttendanceRecordId is not { } recordId)
            {
                return Result<ExcuseSummary>.Fail(AttendanceErrors.RecordNotFound).ToActionResult(HttpContext);
            }

            await using var stream = form.File?.OpenReadStream();
            var upload = form.File is null || stream is null ? null : new DocumentUpload(form.File.FileName, form.File.ContentType, stream);

            var result = await handler.SubmitAsync(new SubmitExcuseCommand(recordId, form.Reason, upload), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/attendance/excuses/{summary.Id}", summary));
        }

        // Give a sectionId (Attendance.Read on the section) or a studentId (their own, or their department's).
        [HttpGet]
        public async Task<IActionResult> List([FromServices] AttendanceExcuseHandler handler, CancellationToken cancellationToken, [FromQuery] Guid? sectionId = null, [FromQuery] Guid? studentId = null, [FromQuery] ExcuseStatus? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize)
            => (await handler.ListAsync(sectionId, studentId, status, new PaginationQuery(page, pageSize), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{excuseId:guid}/review")]
        public async Task<IActionResult> Review(Guid excuseId, [FromBody] ReviewExcuseRequest request, [FromServices] AttendanceExcuseHandler handler, CancellationToken cancellationToken)
        {
            if (request.Accept is not { } accept)
            {
                return Result<ExcuseSummary>.Fail(AttendanceErrors.ExcuseRejectNeedsNote).ToActionResult(HttpContext);
            }

            return (await handler.ReviewAsync(excuseId, new ReviewExcuseCommand(accept, request.Note), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }
    }
}
