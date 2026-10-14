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
    [Route("api/v1/student-charges")]
    [Authorize]
    public sealed class StudentChargesController : ControllerBase
    {
        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] GenerateChargesRequest request, [FromServices] ChargeHandler handler, CancellationToken cancellationToken)
            => (await handler.GenerateAsync(new GenerateChargesCommand(request.AcademicSemesterId, request.AcademicProgramId, request.DueDate), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet]
        public async Task<IActionResult> List([FromServices] ChargeHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] Guid? studentId = null, [FromQuery] Guid? academicSemesterId = null, [FromQuery] ChargeStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), studentId, academicSemesterId, status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] ChargeHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, [FromServices] ChargeHandler handler, CancellationToken cancellationToken)
            => (await handler.CancelAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
