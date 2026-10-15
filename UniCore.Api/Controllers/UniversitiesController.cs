using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.University;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.University;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/universities")]
    [Authorize]
    public sealed class UniversitiesController : ControllerBase
    {
        // Permissions and scope are checked inside the handler.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] UniversityManagementHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, isActive, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{universityId:guid}")]
        public async Task<IActionResult> Get(Guid universityId, [FromServices] UniversityManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(universityId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUniversityRequest request, [FromServices] UniversityManagementHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateUniversityCommand(request.Code, request.Name, request.Description, request.Email, request.Phone, request.Website);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/universities/{summary.Id}", summary));
        }

        [HttpPut("{universityId:guid}")]
        public async Task<IActionResult> Update(Guid universityId, [FromBody] UpdateUniversityRequest request, [FromServices] UniversityManagementHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdateUniversityCommand(request.Name, request.Description, request.Email, request.Phone, request.Website);

            return (await handler.UpdateAsync(universityId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{universityId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid universityId, [FromServices] UniversityManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(universityId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{universityId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid universityId, [FromServices] UniversityManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(universityId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
