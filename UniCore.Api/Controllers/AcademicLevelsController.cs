using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Calendar;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Calendar;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/academic-levels")]
    [Authorize]
    public sealed class AcademicLevelsController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List([FromServices] AcademicLevelHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null, [FromQuery] Guid? academicProgramId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, isActive, academicProgramId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{levelId:guid}")]
        public async Task<IActionResult> Get(Guid levelId, [FromServices] AcademicLevelHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(levelId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAcademicLevelRequest request, [FromServices] AcademicLevelHandler handler, CancellationToken cancellationToken)
        {
            // A missing number becomes 0, which the domain rejects (INVALID_LEVEL_ORDER). Required credits may be 0.
            var command = new CreateAcademicLevelCommand(request.AcademicProgramId ?? Guid.Empty, request.Name, request.Order ?? 0, request.RequiredCredits ?? 0);
            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);
            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/academic-levels/{summary.Id}", summary));
        }

        [HttpPut("{levelId:guid}")]
        public async Task<IActionResult> Update(Guid levelId, [FromBody] UpdateAcademicLevelRequest request, [FromServices] AcademicLevelHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(levelId, new UpdateAcademicLevelCommand(request.Name, request.Order ?? 0, request.RequiredCredits ?? 0), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{levelId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid levelId, [FromServices] AcademicLevelHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(levelId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{levelId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid levelId, [FromServices] AcademicLevelHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(levelId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }



}
