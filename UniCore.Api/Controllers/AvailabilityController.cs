using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Scheduling;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Scheduling;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Scheduling;

namespace UniCore.Api.Controllers
{
    // Weekly availability windows of instructors and rooms. Scheduling.Read to read, Scheduling.Manage to change.
    [ApiController]
    [Route("api/v1/instructors/{instructorId:guid}/availability")]
    [Authorize]
    public sealed class InstructorAvailabilityController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List(Guid instructorId, [FromServices] AvailabilityHandler handler, CancellationToken cancellationToken)
            => (await handler.ListForInstructorAsync(instructorId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Add(Guid instructorId, [FromBody] AddAvailabilityRequest request, [FromServices] AvailabilityHandler handler, CancellationToken cancellationToken)
        {
            if (!AvailabilityRequests.TryRead(request, out var command))
            {
                return Result<AvailabilitySummary>.Fail(SchedulingErrors.TimeInvalid).ToActionResult(HttpContext);
            }

            var result = await handler.AddForInstructorAsync(instructorId, command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/instructors/{instructorId}/availability", summary));
        }

        [HttpDelete("{availabilityId:guid}")]
        public async Task<IActionResult> Remove(Guid instructorId, Guid availabilityId, [FromServices] AvailabilityHandler handler, CancellationToken cancellationToken)
            => (await handler.RemoveForInstructorAsync(instructorId, availabilityId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }

    [ApiController]
    [Route("api/v1/rooms/{roomId:guid}/availability")]
    [Authorize]
    public sealed class RoomAvailabilityController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List(Guid roomId, [FromServices] AvailabilityHandler handler, CancellationToken cancellationToken)
            => (await handler.ListForRoomAsync(roomId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Add(Guid roomId, [FromBody] AddAvailabilityRequest request, [FromServices] AvailabilityHandler handler, CancellationToken cancellationToken)
        {
            if (!AvailabilityRequests.TryRead(request, out var command))
            {
                return Result<AvailabilitySummary>.Fail(SchedulingErrors.TimeInvalid).ToActionResult(HttpContext);
            }

            var result = await handler.AddForRoomAsync(roomId, command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/rooms/{roomId}/availability", summary));
        }

        [HttpDelete("{availabilityId:guid}")]
        public async Task<IActionResult> Remove(Guid roomId, Guid availabilityId, [FromServices] AvailabilityHandler handler, CancellationToken cancellationToken)
            => (await handler.RemoveForRoomAsync(roomId, availabilityId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }

    internal static class AvailabilityRequests
    {
        public static bool TryRead(AddAvailabilityRequest request, out AddAvailabilityCommand command)
        {
            if (request.DayOfWeek is { } day && request.StartTime is { } start && request.EndTime is { } end)
            {
                command = new AddAvailabilityCommand(day, start, end);
                return true;
            }

            command = new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.MinValue, TimeOnly.MinValue);
            return false;
        }
    }
}
