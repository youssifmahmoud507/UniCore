using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Admissions;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Admissions;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/applicants")]
    [Authorize]
    public sealed class ApplicantsController : ControllerBase
    {
        // Permissions are checked inside the handler.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] ApplicantHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{applicantId:guid}")]
        public async Task<IActionResult> Get(Guid applicantId, [FromServices] ApplicantHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(applicantId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateApplicantRequest request, [FromServices] ApplicantHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.CreateAsync(new CreateApplicantCommand(request.FullName, request.Email, request.Phone, request.DateOfBirth), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/applicants/{summary.Id}", summary));
        }

        [HttpPut("{applicantId:guid}")]
        public async Task<IActionResult> Update(Guid applicantId, [FromBody] UpdateApplicantRequest request, [FromServices] ApplicantHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(applicantId, new UpdateApplicantCommand(request.FullName, request.Email, request.Phone, request.DateOfBirth), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
