using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Finance;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Finance;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/scholarships")]
    [Authorize]
    public sealed class ScholarshipsController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateScholarshipRequest request, [FromServices] ScholarshipHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateScholarshipCommand(request.Code, request.Name, request.Description, request.Type, request.Value);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/scholarships/{created.Id}", created));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] ScholarshipHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] bool? isActive = null, [FromQuery] string? search = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), isActive, search, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] ScholarshipHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScholarshipRequest request, [FromServices] ScholarshipHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(id, new UpdateScholarshipCommand(request.Name, request.Description, request.Type, request.Value), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/activate")]
        public async Task<IActionResult> Activate(Guid id, [FromServices] ScholarshipHandler handler, CancellationToken cancellationToken)
            => (await handler.SetActiveAsync(id, true, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id, [FromServices] ScholarshipHandler handler, CancellationToken cancellationToken)
            => (await handler.SetActiveAsync(id, false, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
