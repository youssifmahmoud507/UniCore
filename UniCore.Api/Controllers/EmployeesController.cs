using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.People;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.People;
using UniCore.Domain.Modules.People;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/employees")]
    [Authorize]
    public sealed class EmployeesController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List([FromServices] EmployeeHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] EmployeeStatus? status = null, [FromQuery] Guid? departmentId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, status, departmentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{employeeId:guid}")]
        public async Task<IActionResult> Get(Guid employeeId, [FromServices] EmployeeHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(employeeId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request, [FromServices] EmployeeHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateEmployeeCommand(request.PersonProfileId ?? Guid.Empty, request.EmployeeNumber, request.DepartmentId, request.JobTitle, request.HireDate ?? default);
            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);
            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/employees/{summary.Id}", summary));
        }

        [HttpPut("{employeeId:guid}")]
        public async Task<IActionResult> Update(Guid employeeId, [FromBody] UpdateEmployeeRequest request, [FromServices] EmployeeHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(employeeId, new UpdateEmployeeCommand(request.DepartmentId, request.JobTitle), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{employeeId:guid}/terminate")]
        public async Task<IActionResult> Terminate(Guid employeeId, [FromBody] TerminateEmployeeRequest request, [FromServices] EmployeeHandler handler, CancellationToken cancellationToken)
            => (await handler.TerminateAsync(employeeId, request.TerminationDate ?? default, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
