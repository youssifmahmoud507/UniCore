using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Calendar;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Calendar;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/academic-terms")]
    [Authorize]
    public sealed class AcademicTermsController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List([FromServices] AcademicTermHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, isActive, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{termId:guid}")]
        public async Task<IActionResult> Get(Guid termId, [FromServices] AcademicTermHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(termId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAcademicTermRequest request, [FromServices] AcademicTermHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateAcademicTermCommand(request.Code, request.Name, request.SortOrder ?? 0);
            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);
            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/academic-terms/{summary.Id}", summary));
        }

        [HttpPut("{termId:guid}")]
        public async Task<IActionResult> Update(Guid termId, [FromBody] UpdateAcademicTermRequest request, [FromServices] AcademicTermHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(termId, new UpdateAcademicTermCommand(request.Name, request.SortOrder ?? 0), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{termId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid termId, [FromServices] AcademicTermHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(termId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{termId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid termId, [FromServices] AcademicTermHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(termId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
