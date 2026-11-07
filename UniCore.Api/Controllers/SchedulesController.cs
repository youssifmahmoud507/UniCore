using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Scheduling;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Scheduling;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Scheduling;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/schedules")]
    [Authorize]
    public sealed class SchedulesController : ControllerBase
    {
        // Permissions are checked inside the handlers: Scheduling.Read to read, Scheduling.Manage to change.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] ScheduleHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? sort = null, [FromQuery] Guid? semesterId = null, [FromQuery] ScheduleStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), sort, semesterId, status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{scheduleId:guid}")]
        public async Task<IActionResult> Get(Guid scheduleId, [FromServices] ScheduleHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(scheduleId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateScheduleRequest request, [FromServices] ScheduleHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.CreateAsync(new CreateScheduleCommand(request.AcademicSemesterId ?? Guid.Empty, request.Name), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/schedules/{summary.Id}", summary));
        }

        [HttpPut("{scheduleId:guid}")]
        public async Task<IActionResult> Update(Guid scheduleId, [FromBody] UpdateScheduleRequest request, [FromServices] ScheduleHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(scheduleId, new UpdateScheduleCommand(request.Name), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{scheduleId:guid}/publish")]
        public async Task<IActionResult> Publish(Guid scheduleId, [FromServices] ScheduleHandler handler, CancellationToken cancellationToken)
            => (await handler.PublishAsync(scheduleId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{scheduleId:guid}/archive")]
        public async Task<IActionResult> Archive(Guid scheduleId, [FromServices] ScheduleHandler handler, CancellationToken cancellationToken)
            => (await handler.ArchiveAsync(scheduleId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        // ---------- slots ----------

        [HttpPost("{scheduleId:guid}/slots")]
        public async Task<IActionResult> AddSlot(Guid scheduleId, [FromBody] AddSlotRequest request, [FromServices] ScheduleHandler handler, CancellationToken cancellationToken)
        {
            if (request.DayOfWeek is not { } day || request.StartTime is not { } start || request.EndTime is not { } end)
            {
                return (Result<ScheduleSummary>.Fail(SchedulingErrors.TimeInvalid)).ToActionResult(HttpContext);
            }

            return (await handler.AddSlotAsync(scheduleId, new AddSlotCommand(day, start, end), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpDelete("{scheduleId:guid}/slots/{slotId:guid}")]
        public async Task<IActionResult> RemoveSlot(Guid scheduleId, Guid slotId, [FromServices] ScheduleHandler handler, CancellationToken cancellationToken)
            => (await handler.RemoveSlotAsync(scheduleId, slotId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        // ---------- sections in the schedule ----------

        [HttpGet("{scheduleId:guid}/meetings")]
        public async Task<IActionResult> ListMeetings(Guid scheduleId, [FromServices] SectionScheduleHandler handler, CancellationToken cancellationToken)
            => (await handler.ListForScheduleAsync(scheduleId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{scheduleId:guid}/meetings")]
        public async Task<IActionResult> Assign(Guid scheduleId, [FromBody] AssignSectionRequest request, [FromServices] SectionScheduleHandler handler, CancellationToken cancellationToken)
        {
            var command = new AssignSectionCommand(request.CourseSectionId ?? Guid.Empty, request.ScheduleSlotId ?? Guid.Empty, request.RoomId ?? Guid.Empty);

            var result = await handler.AssignAsync(scheduleId, command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, meeting => Created($"/api/v1/schedules/{scheduleId}/meetings", meeting));
        }

        [HttpDelete("{scheduleId:guid}/meetings/{meetingId:guid}")]
        public async Task<IActionResult> Unassign(Guid scheduleId, Guid meetingId, [FromServices] SectionScheduleHandler handler, CancellationToken cancellationToken)
            => (await handler.UnassignAsync(scheduleId, meetingId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        // The weekly meetings of one section.
        [HttpGet("~/api/v1/course-sections/{sectionId:guid}/meetings")]
        public async Task<IActionResult> ListSectionMeetings(Guid sectionId, [FromServices] SectionScheduleHandler handler, CancellationToken cancellationToken)
            => (await handler.ListForSectionAsync(sectionId, cancellationToken)).ToActionResult(HttpContext);
    }
}
