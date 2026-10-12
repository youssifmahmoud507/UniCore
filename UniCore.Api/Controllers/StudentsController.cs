using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Students;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Students;
using UniCore.Domain.Modules.Students;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/students")]
    [Authorize]
    public sealed class StudentsController : ControllerBase
    {
        // Permissions and scope are checked inside the handler.

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AdmitStudentRequest request, [FromServices] StudentManagementHandler handler,CancellationToken cancellationToken)
        {
            var command = new AdmitStudentCommand(request.PersonProfileId ?? Guid.Empty, request.Email, request.AdmissionDate ?? default, request.ExpectedGraduationDate, request.AcademicProgramId ?? Guid.Empty, request.ProgramCode, request.StudyPlanVersionId ?? Guid.Empty, request.AdmissionAcademicYearId ?? Guid.Empty, request.AdmissionYear ?? 0, request.AcademicLevelId, request.CreateAccount);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, outcome => Created($"/api/v1/students/{outcome.Student.Summary.Id}", outcome));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] StudentManagementHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] StudentAcademicStatus? status = null) => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("me")]
        public async Task<IActionResult> GetMine([FromServices] StudentManagementHandler handler, CancellationToken cancellationToken) => (await handler.GetMineAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{studentId:guid}")]
        public async Task<IActionResult> Get(Guid studentId, [FromServices] StudentManagementHandler handler, CancellationToken cancellationToken) => (await handler.GetAsync(studentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{studentId:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid studentId, [FromBody] ChangeStudentStatusRequest request, [FromServices] StudentManagementHandler handler, CancellationToken cancellationToken) => (await handler.ChangeStatusAsync(studentId, request.NewStatus, request.EffectiveDate, request.Reason, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{studentId:guid}/program")]
        public async Task<IActionResult> ChangeProgram(Guid studentId, [FromBody] ChangeStudentProgramRequest request, [FromServices] StudentManagementHandler handler, CancellationToken cancellationToken) => (await handler.ChangeProgramAsync(studentId, request.AcademicProgramId, request.StudyPlanVersionId, request.AcademicLevelId, request.StartDate, request.Reason, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPatch("{studentId:guid}/academic-profile")]
        public async Task<IActionResult> UpdateAcademicProfile(Guid studentId, [FromBody] UpdateAcademicProfileRequest request, [FromServices] StudentManagementHandler handler, CancellationToken cancellationToken) => (await handler.UpdateAcademicProfileAsync(studentId, request.AcademicLevelId, request.AcademicStanding, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{studentId:guid}/account")]
        public async Task<IActionResult> ProvisionAccount(Guid studentId, [FromBody] ProvisionStudentAccountRequest request, [FromServices] StudentManagementHandler handler,CancellationToken cancellationToken)=> (await handler.ProvisionAccountAsync(studentId, request.Email, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
