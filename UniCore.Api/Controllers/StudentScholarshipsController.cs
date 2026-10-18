using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Finance;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Finance;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/student-scholarships")]
    [Authorize]
    public sealed class StudentScholarshipsController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Award([FromBody] AwardScholarshipRequest request, [FromServices] StudentScholarshipHandler handler, CancellationToken cancellationToken)
        {
            var command = new AwardScholarshipCommand(request.StudentId, request.ScholarshipId, request.ValidFrom, request.ValidTo, request.Reason);

            var result = await handler.AwardAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/student-scholarships/{created.Id}", created));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] StudentScholarshipHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] Guid? studentId = null, [FromQuery] StudentScholarshipStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), studentId, status, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/revoke")]
        public async Task<IActionResult> Revoke(Guid id, [FromServices] StudentScholarshipHandler handler, CancellationToken cancellationToken)
            => (await handler.RevokeAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }

}
