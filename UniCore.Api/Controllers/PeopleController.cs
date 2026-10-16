using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.People;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.People;

namespace UniCore.Api.Controllers
{
    // Person profiles. The national id is returned only to callers who hold People.Manage.
    [ApiController]
    [Route("api/v1/people")]
    [Authorize]
    public sealed class PeopleController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List([FromServices] PersonProfileHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{personId:guid}")]
        public async Task<IActionResult> Get(Guid personId, [FromServices] PersonProfileHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(personId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PersonRequest request, [FromServices] PersonProfileHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreatePersonCommand(request.FirstName, request.LastName, request.DateOfBirth, request.Gender, request.NationalId, request.Email, request.Phone);
            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);
            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/people/{summary.Id}", summary));
        }

        [HttpPut("{personId:guid}")]
        public async Task<IActionResult> Update(Guid personId, [FromBody] PersonRequest request, [FromServices] PersonProfileHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdatePersonCommand(request.FirstName, request.LastName, request.DateOfBirth, request.Gender, request.NationalId, request.Email, request.Phone);
            return (await handler.UpdateAsync(personId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{personId:guid}/link-user")]
        public async Task<IActionResult> LinkUser(Guid personId, [FromBody] LinkUserRequest request, [FromServices] PersonProfileHandler handler, CancellationToken cancellationToken)
            => (await handler.LinkUserAsync(personId, request.UserId ?? Guid.Empty, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
