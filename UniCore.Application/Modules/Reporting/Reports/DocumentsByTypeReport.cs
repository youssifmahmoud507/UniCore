using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // How many documents of each type exist, and how much space they take. Counts only, never the documents.
    public sealed class DocumentsByTypeReport(IReportDataSource data) : IReportProvider
    {
        public const string Code = "DOCUMENTS_BY_TYPE";
        private const decimal BytesPerMegabyte = 1_048_576m;

        private static readonly ReportColumn[] Columns =
        [
            new("typeCode", "Type code", ReportColumnType.Text),
            new("typeName", "Type", ReportColumnType.Text),
            new("sensitive", "Sensitive", ReportColumnType.Boolean),
            new("active", "Active", ReportColumnType.Integer),
            new("deleted", "Deleted", ReportColumnType.Integer),
            new("sizeMegabytes", "Active size (MB)", ReportColumnType.Decimal)
        ];

        private readonly IReportDataSource _data = data;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Documents by type",
            "Documents per type: active, deleted and the space the active ones take. Filter by upload date.",
            "Documents",
            Permissions.Documents.Manage,
            false,
            [ReportParameters.From("Uploaded from"), ReportParameters.To("Uploaded to")],
            Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            var range = ReportRanges.Read(arguments);

            if (!range.TryGetValue(out var span))
            {
                return Result<ReportTable>.Fail(range.Errors);
            }

            var from = span.From is { } start ? ReportRanges.StartOf(start) : (DateTimeOffset?)null;
            var toExclusive = span.To is { } end ? ReportRanges.StartOfNext(end) : (DateTimeOffset?)null;
            var loaded = await _data.GetDocumentStatsAsync(from, toExclusive, cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            return ReportTable.Paged(Columns, Build(stats), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<DocumentStat> stats)
            => [.. stats
                .GroupBy(s => (s.TypeCode, s.TypeName, s.IsSensitive))
                .OrderBy(g => g.Key.TypeCode, StringComparer.Ordinal)
                .Select(g => (IReadOnlyList<object?>)new object?[]
                {
                    g.Key.TypeCode,
                    g.Key.TypeName,
                    g.Key.IsSensitive,
                    g.Where(s => s.Status == DocumentStatus.Active).Sum(s => s.Count),
                    g.Where(s => s.Status == DocumentStatus.Deleted).Sum(s => s.Count),
                    Math.Round(g.Where(s => s.Status == DocumentStatus.Active).Sum(s => s.SizeBytes) / BytesPerMegabyte, 2)
                })];
    }



}
