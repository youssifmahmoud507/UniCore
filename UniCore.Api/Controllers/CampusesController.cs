using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.University;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.University;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/campuses")]
    [Authorize]
    public sealed class CampusesController : ControllerBase
    {
        // Permissions and scope are checked inside the handler.

        private static AddressInput? ToInput(AddressRequest? address)
            => address is null ? null : new AddressInput(address.Street, address.City, address.State, address.PostalCode, address.Country);

        [HttpGet]
        public async Task<IActionResult> List([FromServices] CampusManagementHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null, [FromQuery] Guid? universityId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, isActive, universityId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{campusId:guid}")]
        public async Task<IActionResult> Get(Guid campusId, [FromServices] CampusManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(campusId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCampusRequest request, [FromServices] CampusManagementHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateCampusCommand(request.UniversityId ?? Guid.Empty, request.Code, request.Name, ToInput(request.Address), request.TimeZone);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/campuses/{summary.Id}", summary));
        }

        [HttpPut("{campusId:guid}")]
        public async Task<IActionResult> Update(Guid campusId, [FromBody] UpdateCampusRequest request, [FromServices] CampusManagementHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdateCampusCommand(request.Name, ToInput(request.Address), request.TimeZone);

            return (await handler.UpdateAsync(campusId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{campusId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid campusId, [FromServices] CampusManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(campusId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{campusId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid campusId, [FromServices] CampusManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(campusId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
