using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Examination;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Examination;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Examination;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/exams")]
    [Authorize]
    public sealed class ExamsController : ControllerBase
    {
        // Permissions are checked inside the handler: Exams.Read to read, Exams.Manage (on the section's department) to change.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] ExamHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] Guid? sessionId = null, [FromQuery] Guid? sectionId = null, [FromQuery] ExamStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), sessionId, sectionId, status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{examId:guid}")]
        public async Task<IActionResult> Get(Guid examId, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(examId, cancellationToken)).ToActionResult(HttpContext);

        // The registered students split into eligible and blocked by attendance.
        [HttpGet("{examId:guid}/roster")]
        public async Task<IActionResult> Roster(Guid examId, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
            => (await handler.GetRosterAsync(examId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateExamRequest request, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
        {
            if (request.ExamType is not { } type || request.MaxScore is not { } max || request.DurationMinutes is not { } minutes)
            {
                return Result<ExamSummary>.Fail(ExaminationErrors.ExamInvalid).ToActionResult(HttpContext);
            }

            var result = await handler.CreateAsync(new CreateExamCommand(request.CourseSectionId ?? Guid.Empty, request.ExamSessionId ?? Guid.Empty, type, max, minutes), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/exams/{summary.Id}", summary));
        }

        [HttpPut("{examId:guid}")]
        public async Task<IActionResult> Update(Guid examId, [FromBody] UpdateExamRequest request, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
        {
            if (request.MaxScore is not { } max || request.DurationMinutes is not { } minutes)
            {
                return Result<ExamSummary>.Fail(ExaminationErrors.ExamInvalid).ToActionResult(HttpContext);
            }

            return (await handler.UpdateAsync(examId, new UpdateExamCommand(max, minutes), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPut("{examId:guid}/schedule")]
        public async Task<IActionResult> SetSchedule(Guid examId, [FromBody] SetExamScheduleRequest request, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
        {
            if (request.ExamDate is not { } date || request.StartTime is not { } start || request.EndTime is not { } end)
            {
                return Result<ExamSummary>.Fail(ExaminationErrors.ScheduleInvalid).ToActionResult(HttpContext);
            }

            return (await handler.SetScheduleAsync(examId, new SetExamScheduleCommand(date, start, end), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{examId:guid}/rooms")]
        public async Task<IActionResult> AddRoom(Guid examId, [FromBody] AddExamRoomRequest request, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
            => (await handler.AddRoomAsync(examId, new AddExamRoomCommand(request.RoomId ?? Guid.Empty), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpDelete("{examId:guid}/rooms/{roomId:guid}")]
        public async Task<IActionResult> RemoveRoom(Guid examId, Guid roomId, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
            => (await handler.RemoveRoomAsync(examId, roomId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{examId:guid}/invigilators")]
        public async Task<IActionResult> AddInvigilator(Guid examId, [FromBody] AddInvigilatorRequest request, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
        {
            if (request.Role is not { } role)
            {
                return Result<ExamSummary>.Fail(ExaminationErrors.InvigilatorInvalid).ToActionResult(HttpContext);
            }

            return (await handler.AddInvigilatorAsync(examId, new AddInvigilatorCommand(request.RoomId ?? Guid.Empty, request.InstructorId ?? Guid.Empty, role), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpDelete("{examId:guid}/invigilators/{instructorId:guid}")]
        public async Task<IActionResult> RemoveInvigilator(Guid examId, Guid instructorId, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
            => (await handler.RemoveInvigilatorAsync(examId, instructorId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{examId:guid}/publish")]
        public async Task<IActionResult> Publish(Guid examId, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
            => (await handler.PublishAsync(examId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{examId:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid examId, [FromServices] ExamHandler handler, CancellationToken cancellationToken)
            => (await handler.CancelAsync(examId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
