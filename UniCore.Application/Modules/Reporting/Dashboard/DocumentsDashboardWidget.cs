using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Dashboard
{
    public sealed class DocumentsDashboardWidget(IReportDataSource data, IClock clock) : IDashboardWidget
    {
        public const int RecentDays = 7;

        private readonly IReportDataSource _data = data;
        private readonly IClock _clock = clock;

        public string Code => "documents";

        public string Title => "Documents";

        public string Category => "Documents";

        public string Permission => Permissions.Documents.Manage;

        public bool IsPerUser => false;

        public async Task<Result<IReadOnlyList<DashboardMetric>>> LoadAsync(CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var all = await _data.GetDocumentStatsAsync(null, null, cancellationToken);

            if (!all.TryGetValue(out var total))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(all.Errors);
            }

            var recent = await _data.GetDocumentStatsAsync(now.AddDays(-RecentDays), null, cancellationToken);

            if (!recent.TryGetValue(out var uploaded))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(recent.Errors);
            }

            var audit = await _data.GetAuditStatsAsync(now.AddDays(-RecentDays), now.AddMinutes(1), cancellationToken);

            if (!audit.TryGetValue(out var events))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(audit.Errors);
            }

            return Result<IReadOnlyList<DashboardMetric>>.Ok(Build(total, uploaded, events));
        }

        public static IReadOnlyList<DashboardMetric> Build(IReadOnlyList<DocumentStat> all, IReadOnlyList<DocumentStat> uploadedRecently, IReadOnlyList<AuditStat> recentAudit)
            =>
            [
                new("activeDocuments", "Active documents", all.Where(s => s.Status == DocumentStatus.Active).Sum(s => s.Count)),
                new("sensitiveDocuments", "Sensitive documents", all.Where(s => s.Status == DocumentStatus.Active && s.IsSensitive).Sum(s => s.Count)),
                new("uploadedRecently", $"Uploaded in the last {RecentDays} days", uploadedRecently.Where(s => s.Status == DocumentStatus.Active).Sum(s => s.Count)),
                new("sensitiveReadsRecently", $"Sensitive reads in the last {RecentDays} days", recentAudit.Where(a => a.Action == AuditActions.DocumentAccessed).Sum(a => a.Count))
            ];
    }


}
