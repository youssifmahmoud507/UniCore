using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Examination;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Examination;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Examination;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/exam-sessions")]
    [Authorize]
    public sealed class ExamSessionsController : ControllerBase
    {
        // Permissions are checked inside the handler: Exams.Read to read, Exams.Manage to create and close.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] ExamSessionHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] Guid? semesterId = null, [FromQuery] ExamSessionStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), semesterId, status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{sessionId:guid}")]
        public async Task<IActionResult> Get(Guid sessionId, [FromServices] ExamSessionHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(sessionId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateExamSessionRequest request, [FromServices] ExamSessionHandler handler, CancellationToken cancellationToken)
        {
            if (request.ExamType is not { } type || request.StartDate is not { } start || request.EndDate is not { } end)
            {
                return Result<ExamSessionSummary>.Fail(ExaminationErrors.SessionInvalid).ToActionResult(HttpContext);
            }

            var result = await handler.CreateAsync(new CreateExamSessionCommand(request.AcademicSemesterId ?? Guid.Empty, request.Name, type, start, end), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/exam-sessions/{summary.Id}", summary));
        }

        [HttpPost("{sessionId:guid}/close")]
        public async Task<IActionResult> Close(Guid sessionId, [FromServices] ExamSessionHandler handler, CancellationToken cancellationToken)
            => (await handler.CloseAsync(sessionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
