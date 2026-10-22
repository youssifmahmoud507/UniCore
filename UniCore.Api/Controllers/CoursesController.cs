using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Courses;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Courses;
using UniCore.Domain.Modules.Courses;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/courses")]
    [Authorize]
    public sealed class CoursesController : ControllerBase
    {
        // Permissions and scope are checked inside the handlers:
        // Courses.Read to read, Courses.Manage (in the course's department) to change.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] CourseHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null, [FromQuery] Guid? departmentId = null, [FromQuery] CourseType? courseType = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, isActive, departmentId, courseType, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{courseId:guid}")]
        public async Task<IActionResult> Get(Guid courseId, [FromServices] CourseHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(courseId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCourseRequest request, [FromServices] CourseHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateCourseCommand(request.DepartmentId ?? Guid.Empty, request.Code, request.Title, request.Description, request.CourseType);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/courses/{summary.Id}", summary));
        }

        [HttpPut("{courseId:guid}")]
        public async Task<IActionResult> Update(Guid courseId, [FromBody] UpdateCourseRequest request, [FromServices] CourseHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(courseId, new UpdateCourseCommand(request.Title, request.Description, request.CourseType), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{courseId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid courseId, [FromServices] CourseHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(courseId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{courseId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid courseId, [FromServices] CourseHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(courseId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        // ---------- versions of a course ----------

        [HttpGet("{courseId:guid}/versions")]
        public async Task<IActionResult> ListVersions(Guid courseId, [FromServices] CourseVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.ListAsync(courseId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{courseId:guid}/versions")]
        public async Task<IActionResult> CreateVersion(Guid courseId, [FromBody] CreateCourseVersionRequest request, [FromServices] CourseVersionHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateCourseVersionCommand(request.SourceVersionId, request.Title, request.Description, request.Credits, request.LectureHours, request.PracticalHours, request.EffectiveFrom);

            var result = await handler.CreateAsync(courseId, command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, version => Created($"/api/v1/course-versions/{version.Id}", version));
        }
    }
}
