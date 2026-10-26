using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Documents;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Documents;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/document-types")]
    [Authorize]
    public sealed class DocumentTypesController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List([FromServices] DocumentTypeHandler handler, CancellationToken cancellationToken)
            => (await handler.ListAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDocumentTypeRequest request, [FromServices] DocumentTypeHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.CreateAsync(new CreateDocumentTypeCommand(request.Code, request.Name, request.IsSensitive, request.AllowedFormats), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/document-types/{created.Id}", created));
        }
    }



}
