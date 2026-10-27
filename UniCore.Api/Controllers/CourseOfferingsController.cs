using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Offerings;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/course-offerings")]
    [Authorize]
    public sealed class CourseOfferingsController : ControllerBase
    {
        // Permissions and scope are checked inside the handlers:
        // Courses.Read to read, Courses.Manage (in the offered course's department) to change.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] CourseOfferingHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? sort = null, [FromQuery] Guid? semesterId = null, [FromQuery] Guid? courseVersionId = null, [FromQuery] CourseOfferingStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), sort, semesterId, courseVersionId, status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{offeringId:guid}")]
        public async Task<IActionResult> Get(Guid offeringId, [FromServices] CourseOfferingHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(offeringId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOfferingRequest request, [FromServices] CourseOfferingHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateOfferingCommand(request.CourseVersionId ?? Guid.Empty, request.AcademicSemesterId ?? Guid.Empty, request.MaxSections);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/course-offerings/{summary.Id}", summary));
        }

        [HttpPut("{offeringId:guid}")]
        public async Task<IActionResult> Update(Guid offeringId, [FromBody] UpdateOfferingRequest request, [FromServices] CourseOfferingHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(offeringId, new UpdateOfferingCommand(request.MaxSections), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{offeringId:guid}/open")]
        public async Task<IActionResult> Open(Guid offeringId, [FromServices] CourseOfferingHandler handler, CancellationToken cancellationToken)
            => (await handler.OpenAsync(offeringId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{offeringId:guid}/close")]
        public async Task<IActionResult> Close(Guid offeringId, [FromServices] CourseOfferingHandler handler, CancellationToken cancellationToken)
            => (await handler.CloseAsync(offeringId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{offeringId:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid offeringId, [FromServices] CourseOfferingHandler handler, CancellationToken cancellationToken)
            => (await handler.CancelAsync(offeringId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        // ---------- sections of an offering ----------

        [HttpGet("{offeringId:guid}/sections")]
        public async Task<IActionResult> ListSections(Guid offeringId, [FromServices] CourseSectionHandler handler, CancellationToken cancellationToken)
            => (await handler.ListAsync(offeringId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{offeringId:guid}/sections")]
        public async Task<IActionResult> CreateSection(Guid offeringId, [FromBody] CreateSectionRequest request, [FromServices] CourseSectionHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.CreateAsync(offeringId, new CreateSectionCommand(request.SectionCode, request.Capacity), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/course-sections/{summary.Id}", summary));
        }
    }
}
