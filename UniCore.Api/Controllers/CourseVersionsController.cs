using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Courses;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Courses;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/course-versions")]
    [Authorize]
    public sealed class CourseVersionsController : ControllerBase
    {
        // Permissions and scope are checked inside the handler. Only a Draft version can be changed.

        [HttpGet("{versionId:guid}")]
        public async Task<IActionResult> Get(Guid versionId, [FromServices] CourseVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(versionId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("{versionId:guid}")]
        public async Task<IActionResult> Update(Guid versionId, [FromBody] UpdateCourseVersionRequest request, [FromServices] CourseVersionHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdateCourseVersionCommand(request.Title, request.Description, request.Credits, request.LectureHours, request.PracticalHours, request.EffectiveFrom);

            return (await handler.UpdateAsync(versionId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{versionId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid versionId, [FromServices] CourseVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(versionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{versionId:guid}/retire")]
        public async Task<IActionResult> Retire(Guid versionId, [FromBody] RetireCourseVersionRequest request, [FromServices] CourseVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.RetireAsync(versionId, new RetireCourseVersionCommand(request.EffectiveTo), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
