using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniCore.Api.Extensions;
using UniCore.Api.Security;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Reporting;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Api.Controllers
{
    // Reports: the catalog, one report, a page of its rows, and the same report as a CSV, Excel or PDF file.
    // The filters of a report are query parameters (?from=2026-01-01&status=Paid); the handler checks each of them
    // against the parameters the report declares.
    [ApiController]
    [Route("api/v1/reports")]
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class ReportsController : ControllerBase
    {
        // Query parameters that belong to the endpoint, not to the report.
        private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase) { "page", "pageSize", "format" };

        [HttpGet]
        [HasPermission(Permissions.Reports.Read)]
        public async Task<IActionResult> List([FromServices] ReportHandler handler, CancellationToken cancellationToken)
            => (await handler.ListAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{code}")]
        [HasPermission(Permissions.Reports.Read)]
        public async Task<IActionResult> Describe(string code, [FromServices] ReportHandler handler, CancellationToken cancellationToken)
            => (await handler.DescribeAsync(code, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{code}/run")]
        [HasPermission(Permissions.Reports.Read)]
        public async Task<IActionResult> Run(string code, [FromServices] ReportHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize)
            => (await handler.RunAsync(code, Filters(), new PaginationQuery(page, pageSize), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{code}/export")]
        [HasPermission(Permissions.Reports.Export)]
        [EnableRateLimiting(RateLimitPolicies.Export)]
        public async Task<IActionResult> Export(string code, [FromServices] ReportHandler handler, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            var result = await handler.ExportAsync(code, format, Filters(), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, file =>
            {
                // Always a download, never shown as a page, never sniffed, never kept by a cache or a proxy.
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                Response.Headers["Cache-Control"] = "private, no-store";

                return File(file.Content, file.ContentType, file.FileName);
            });
        }

        private Dictionary<string, string?> Filters()
        {
            var filters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var parameter in Request.Query)
            {
                if (!Reserved.Contains(parameter.Key))
                {
                    filters[parameter.Key] = parameter.Value.ToString();
                }
            }

            return filters;
        }
    }

}
