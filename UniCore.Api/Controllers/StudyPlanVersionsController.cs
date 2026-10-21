using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Curriculum;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Curriculum;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/study-plan-versions")]
    [Authorize]
    public sealed class StudyPlanVersionsController : ControllerBase
    {
        // Permissions and scope are checked inside the handler. Only a Draft version can be changed.

        [HttpGet("{versionId:guid}")]
        public async Task<IActionResult> Get(Guid versionId, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(versionId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("{versionId:guid}")]
        public async Task<IActionResult> Update(Guid versionId, [FromBody] UpdateVersionRequest request, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(versionId, new UpdateVersionCommand(request.EffectiveFrom), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{versionId:guid}/courses")]
        public async Task<IActionResult> AddCourse(Guid versionId, [FromBody] AddPlanCourseRequest request, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
        {
            var command = new AddPlanCourseCommand(request.CourseVersionId ?? Guid.Empty, request.AcademicLevelId, request.RecommendedTermId, request.RequirementType, request.RequiredCredits);

            return (await handler.AddCourseAsync(versionId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPut("{versionId:guid}/courses/{courseId:guid}")]
        public async Task<IActionResult> UpdateCourse(Guid versionId, Guid courseId, [FromBody] UpdatePlanCourseRequest request, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdatePlanCourseCommand(request.AcademicLevelId, request.RecommendedTermId, request.RequirementType, request.RequiredCredits);

            return (await handler.UpdateCourseAsync(versionId, courseId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpDelete("{versionId:guid}/courses/{courseId:guid}")]
        public async Task<IActionResult> RemoveCourse(Guid versionId, Guid courseId, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.RemoveCourseAsync(versionId, courseId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("{versionId:guid}/requirements")]
        public async Task<IActionResult> SetRequirement(Guid versionId, [FromBody] SetRequirementRequest request, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.SetRequirementAsync(versionId, new SetRequirementCommand(request.CourseVersionId ?? Guid.Empty, request.RequirementType), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpDelete("{versionId:guid}/requirements/{courseVersionId:guid}")]
        public async Task<IActionResult> RemoveRequirement(Guid versionId, Guid courseVersionId, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.RemoveRequirementAsync(versionId, courseVersionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{versionId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid versionId, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(versionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{versionId:guid}/retire")]
        public async Task<IActionResult> Retire(Guid versionId, [FromBody] RetireVersionRequest request, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.RetireAsync(versionId, new RetireVersionCommand(request.EffectiveTo), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
