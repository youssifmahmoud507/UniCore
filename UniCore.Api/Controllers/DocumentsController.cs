using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Documents;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Documents;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/documents")]
    [Authorize]
    public sealed class DocumentsController : ControllerBase
    {
        // A little above the 10 MB file limit, for the form itself. The real limit is checked while reading.
        private const long MaxFormBytes = 12_582_912;

        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFormBytes)]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentForm form, [FromServices] DocumentHandler handler, CancellationToken cancellationToken)
        {
            if (form.File is null)
            {
                return Result<DocumentDto>.Fail(DocumentErrors.DataInvalid).ToActionResult(HttpContext);
            }

            await using var stream = form.File.OpenReadStream();

            var result = await handler.UploadAsync(form.TypeCode, new DocumentUpload(form.File.FileName, form.File.ContentType, stream), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/documents/{created.Id}", created));
        }

        // The documents of the signed-in person.
        [HttpGet]
        public async Task<IActionResult> ListMine([FromServices] DocumentHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize)
            => (await handler.ListMineAsync(new PaginationQuery(page, pageSize), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] DocumentHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id, [FromServices] DocumentHandler handler, CancellationToken cancellationToken, [FromQuery] int? version = null)
        {
            var result = await handler.DownloadAsync(id, version, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, content =>
            {
                // Always a download (attachment), never shown by the browser as a page, and never sniffed.
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                Response.Headers["Cache-Control"] = "private, no-store";

                return File(content.Content, content.ContentType, content.FileName);
            });
        }

        [HttpPost("{id:guid}/versions")]
        [Consumes("multipart/form-data")]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFormBytes)]
        public async Task<IActionResult> AddVersion(Guid id, [FromForm] UploadVersionForm form, [FromServices] DocumentHandler handler, CancellationToken cancellationToken)
        {
            if (form.File is null)
            {
                return Result<DocumentDto>.Fail(DocumentErrors.DataInvalid).ToActionResult(HttpContext);
            }

            await using var stream = form.File.OpenReadStream();

            var result = await handler.AddVersionAsync(id, new DocumentUpload(form.File.FileName, form.File.ContentType, stream), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, added => Created($"/api/v1/documents/{added.Id}", added));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, [FromServices] DocumentHandler handler, CancellationToken cancellationToken)
            => (await handler.DeleteAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}/access")]
        public async Task<IActionResult> ListAccess(Guid id, [FromServices] DocumentHandler handler, CancellationToken cancellationToken)
            => (await handler.ListAccessAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/access")]
        public async Task<IActionResult> GrantAccess(Guid id, [FromBody] GrantAccessRequest request, [FromServices] DocumentHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.GrantAccessAsync(id, new GrantAccessCommand(request.UserId, request.AccessType, request.ExpiresAt), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/documents/{id}/access", created));
        }

        [HttpDelete("{id:guid}/access/{accessId:guid}")]
        public async Task<IActionResult> RevokeAccess(Guid id, Guid accessId, [FromServices] DocumentHandler handler, CancellationToken cancellationToken)
            => (await handler.RevokeAccessAsync(id, accessId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }



}
