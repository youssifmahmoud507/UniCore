using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Reporting;

namespace UniCore.Application.Modules.Reporting
{
    // List, describe, run and export reports. Every report needs Reports.Read AND its own permission; exporting needs
    // Reports.Export too. A permission is checked without a resource here: a report that shows scoped data filters
    // by the user's scopes itself.
    public sealed class ReportHandler(IPermissionAuthorizer authorizer, ReportCatalog catalog, IEnumerable<IReportExporter> exporters, IAuditWriter audit, IClock clock)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ReportCatalog _catalog = catalog;
        private readonly IReadOnlyList<IReportExporter> _exporters = [.. exporters];
        private readonly IAuditWriter _audit = audit;
        private readonly IClock _clock = clock;

        public async Task<Result<IReadOnlyList<ReportSummaryDto>>> ListAsync(CancellationToken cancellationToken = default)
        {
            var read = await _authorizer.AuthorizeAsync(Permissions.Reports.Read, null, cancellationToken);

            if (read.IsFailure)
            {
                return Result<IReadOnlyList<ReportSummaryDto>>.Fail(read.Errors);
            }

            var visible = new List<ReportSummaryDto>();

            foreach (var provider in _catalog.All)
            {
                var descriptor = provider.Descriptor;

                if ((await _authorizer.AuthorizeAsync(descriptor.Permission, null, cancellationToken)).IsSuccess)
                {
                    visible.Add(new ReportSummaryDto(descriptor.Code, descriptor.Name, descriptor.Description, descriptor.Category, descriptor.IsSensitive));
                }
            }

            return Result<IReadOnlyList<ReportSummaryDto>>.Ok(visible);
        }

        public async Task<Result<ReportDescriptorDto>> DescribeAsync(string code, CancellationToken cancellationToken = default)
        {
            var found = await FindAuthorizedAsync(code, false, cancellationToken);

            if (!found.TryGetValue(out var provider))
            {
                return Result<ReportDescriptorDto>.Fail(found.Errors);
            }

            var descriptor = provider.Descriptor;
            var canExport = (await _authorizer.AuthorizeAsync(Permissions.Reports.Export, null, cancellationToken)).IsSuccess;

            return new ReportDescriptorDto(descriptor.Code, descriptor.Name, descriptor.Description, descriptor.Category, descriptor.IsSensitive, descriptor.Parameters, descriptor.Columns, canExport ? [.. _exporters.Select(e => e.Format.ToString().ToLowerInvariant())] : [], canExport);
        }

        public async Task<Result<ReportResultDto>> RunAsync(string code, IReadOnlyDictionary<string, string?> parameters, PaginationQuery paging, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var found = await FindAuthorizedAsync(code, false, cancellationToken);

            if (!found.TryGetValue(out var provider))
            {
                return Result<ReportResultDto>.Fail(found.Errors);
            }

            var descriptor = provider.Descriptor;
            var parsed = ReportArgumentParser.Parse(descriptor, parameters);

            if (!parsed.TryGetValue(out var arguments))
            {
                return Result<ReportResultDto>.Fail(parsed.Errors);
            }

            var ran = await provider.RunAsync(arguments, new ReportPage(paging.Skip, paging.NormalizedPageSize), cancellationToken);

            if (!ran.TryGetValue(out var table))
            {
                return Result<ReportResultDto>.Fail(ran.Errors);
            }

            if (descriptor.IsSensitive)
            {
                await _audit.WriteAsync(new AuditEntry(AuditActions.ReportViewed, null, "Report", descriptor.Code, request.IpAddress, request.UserAgent, null, arguments.Summary), cancellationToken);
            }

            return new ReportResultDto(descriptor.Code, descriptor.Name, table.Columns, table.Rows, paging.NormalizedPage, paging.NormalizedPageSize, table.TotalRows, _clock.UtcNow);
        }

        public async Task<Result<ExportedFile>> ExportAsync(string code, string? format, IReadOnlyDictionary<string, string?> parameters, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var found = await FindAuthorizedAsync(code, true, cancellationToken);

            if (!found.TryGetValue(out var provider))
            {
                return Result<ExportedFile>.Fail(found.Errors);
            }

            var exporter = FindExporter(format);

            if (exporter is null)
            {
                return ReportingErrors.FormatUnsupported;
            }

            var descriptor = provider.Descriptor;
            var parsed = ReportArgumentParser.Parse(descriptor, parameters);

            if (!parsed.TryGetValue(out var arguments))
            {
                return Result<ExportedFile>.Fail(parsed.Errors);
            }

            // One row more than the limit tells whether the report is too big without reading all of it.
            var ran = await provider.RunAsync(arguments, ReportPage.First(exporter.MaxRows + 1), cancellationToken);

            if (!ran.TryGetValue(out var table))
            {
                return Result<ExportedFile>.Fail(ran.Errors);
            }

            if (table.TotalRows > exporter.MaxRows)
            {
                return ReportingErrors.ExportTooLarge(exporter.MaxRows, exporter.Format.ToString().ToUpperInvariant());
            }

            var now = _clock.UtcNow;
            var summary = arguments.Summary;
            var content = exporter.Export(new ReportExportModel(descriptor.Name, descriptor.Description, summary.Length == 0 ? "No filters" : summary, now, table));
            var fileName = $"{descriptor.Code.ToLowerInvariant().Replace('_', '-')}-{now:yyyyMMdd-HHmm}.{exporter.Extension}";

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.ReportExported, null, "Report", descriptor.Code, request.IpAddress, request.UserAgent, null, $"format={exporter.Format.ToString().ToLowerInvariant()}; rows={table.TotalRows}; {summary}".TrimEnd(' ', ';')),
                cancellationToken);

            return new ExportedFile(fileName, exporter.ContentType, content);
        }

        // Reports.Read, then the report's own permission (and Reports.Export for an export). An unknown code is 404.
        private async Task<Result<IReportProvider>> FindAuthorizedAsync(string code, bool export, CancellationToken cancellationToken)
        {
            var read = await _authorizer.AuthorizeAsync(Permissions.Reports.Read, null, cancellationToken);

            if (read.IsFailure)
            {
                return Result<IReportProvider>.Fail(read.Errors);
            }

            var provider = _catalog.Find(code);

            if (provider is null)
            {
                return ReportingErrors.NotFound;
            }

            var own = await _authorizer.AuthorizeAsync(provider.Descriptor.Permission, null, cancellationToken);

            if (own.IsFailure)
            {
                return Result<IReportProvider>.Fail(own.Errors);
            }

            if (export)
            {
                var allowed = await _authorizer.AuthorizeAsync(Permissions.Reports.Export, null, cancellationToken);

                if (allowed.IsFailure)
                {
                    return Result<IReportProvider>.Fail(allowed.Errors);
                }
            }

            return Result<IReportProvider>.Ok(provider);
        }

        private IReportExporter? FindExporter(string? format) => format?.Trim().ToLowerInvariant() switch
        {
            "csv" => _exporters.FirstOrDefault(e => e.Format == ReportFormat.Csv),
            "xlsx" => _exporters.FirstOrDefault(e => e.Format == ReportFormat.Xlsx),
            "pdf" => _exporters.FirstOrDefault(e => e.Format == ReportFormat.Pdf),
            _ => null
        };
    }




}
