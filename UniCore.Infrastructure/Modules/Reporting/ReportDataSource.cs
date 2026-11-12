using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using UniCore.Application.Modules.Reporting;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Notifications;
using UniCore.Domain.Modules.Reporting;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.Workflow;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Reporting
{
    // Read-only queries for the reports and the dashboard. Everything is AsNoTracking and projected to the flat facts the
    // reports need. Counting is done by the database (GROUP BY); only the lists that need per-row logic (invoices,
    // payments, requests) are read as rows, and refuse to grow past MaxSourceRows.
    //
    // Money is stored through a value converter, so it is read as Money and added up here, not in SQL.
    public sealed class ReportDataSource(AppDbContext db, ILogger<ReportDataSource> logger) : IReportDataSource
    {
        public const int MaxSourceRows = 200_000;

        private readonly AppDbContext _db = db;
        private readonly ILogger<ReportDataSource> _logger = logger;

        public Task<Result<IReadOnlyList<InvoiceFact>>> GetInvoicesAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) => ReadAsync<InvoiceFact>(async () =>
            {
                var invoices = _db.Set<Invoice>().AsNoTracking().AsQueryable();

                if (from is { } start)
                {
                    invoices = invoices.Where(i => i.IssueDate >= start);
                }

                if (to is { } end)
                {
                    invoices = invoices.Where(i => i.IssueDate <= end);
                }

                var rows = await invoices.Join(_db.Set<Student>().AsNoTracking(), i => i.StudentId, s => s.Id, (i, s) => new { i.StudentId, s.StudentNumber, i.IssueDate, i.DueDate, i.Status, i.TotalAmount, i.PaidAmount, i.Balance }).Take(MaxSourceRows + 1).ToListAsync(cancellationToken);

                if (rows.Count > MaxSourceRows)
                {
                    return ReportingErrors.SourceTooLarge;
                }

                return Result<IReadOnlyList<InvoiceFact>>.Ok([.. rows.Select(r => new InvoiceFact(r.StudentId, r.StudentNumber, r.IssueDate, r.DueDate, r.Status, r.TotalAmount.Amount, r.PaidAmount.Amount, r.Balance.Amount))]);
            });

        public Task<Result<IReadOnlyList<OpenInvoiceFact>>> GetOpenInvoicesAsync(CancellationToken cancellationToken = default) => ReadAsync<OpenInvoiceFact>(async () =>
            {
                var rows = await _db.Set<Invoice>().AsNoTracking().Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid).Select(i => new { i.DueDate, i.Balance }).Take(MaxSourceRows + 1).ToListAsync(cancellationToken);

                if (rows.Count > MaxSourceRows)
                {
                    return ReportingErrors.SourceTooLarge;
                }

                return Result<IReadOnlyList<OpenInvoiceFact>>.Ok([.. rows.Where(r => r.Balance.Amount > 0m).Select(r => new OpenInvoiceFact(r.DueDate, r.Balance.Amount))]);
            });

        public Task<Result<IReadOnlyList<PaymentFact>>> GetPaymentsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => ReadAsync<PaymentFact>(async () =>
            {
                var rows = await _db.Set<Payment>().AsNoTracking().Where(p => p.PaymentDate >= from && p.PaymentDate <= to).Select(p => new { p.PaymentDate, p.Method, p.Amount, p.RefundedAmount }).Take(MaxSourceRows + 1).ToListAsync(cancellationToken);

                if (rows.Count > MaxSourceRows)
                {
                    return ReportingErrors.SourceTooLarge;
                }

                return Result<IReadOnlyList<PaymentFact>>.Ok([.. rows.Select(r => new PaymentFact(r.PaymentDate, r.Method, r.Amount.Amount, r.RefundedAmount.Amount))]);
            });

        public Task<Result<IReadOnlyList<WorkflowFact>>> GetWorkflowsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default) => ReadAsync<WorkflowFact>(async () =>
            {
                var instances = _db.Set<WorkflowInstance>().AsNoTracking().AsQueryable();

                if (from is { } start)
                {
                    instances = instances.Where(i => i.StartedAt >= start);
                }

                if (toExclusive is { } end)
                {
                    instances = instances.Where(i => i.StartedAt < end);
                }

                var rows = await instances.Select(i => new { i.DefinitionCode, i.Status, i.ScopeType, i.ScopeId, i.StartedAt, i.CompletedAt }).Take(MaxSourceRows + 1).ToListAsync(cancellationToken);

                if (rows.Count > MaxSourceRows)
                {
                    return ReportingErrors.SourceTooLarge;
                }

                return Result<IReadOnlyList<WorkflowFact>>.Ok([.. rows.Select(r => new WorkflowFact(r.DefinitionCode, r.Status, r.ScopeType, r.ScopeId, r.StartedAt, r.CompletedAt))]);
            });

        public Task<Result<IReadOnlyList<WorkflowStat>>> GetWorkflowStatsAsync(DateTimeOffset? completedSince, CancellationToken cancellationToken = default) => ReadAsync<WorkflowStat>(async () =>
            {
                var instances = _db.Set<WorkflowInstance>().AsNoTracking().AsQueryable();

                if (completedSince is { } since)
                {
                    instances = instances.Where(i => i.Status == WorkflowInstanceStatus.InProgress || i.Status == WorkflowInstanceStatus.Returned || (i.CompletedAt != null && i.CompletedAt >= since));
                }

                var groups = await instances.GroupBy(i => new { i.Status, i.ScopeType, i.ScopeId }).Select(g => new { g.Key.Status, g.Key.ScopeType, g.Key.ScopeId, Count = g.Count() }).ToListAsync(cancellationToken);

                return Result<IReadOnlyList<WorkflowStat>>.Ok([.. groups.Select(g => new WorkflowStat(g.Status, g.ScopeType, g.ScopeId, g.Count))]);
            });

        public Task<Result<IReadOnlyList<DocumentStat>>> GetDocumentStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default) => ReadAsync<DocumentStat>(async () =>
            {
                var documents = _db.Set<Document>().AsNoTracking().AsQueryable();

                if (from is { } start)
                {
                    documents = documents.Where(d => d.CreatedAt >= start);
                }

                if (toExclusive is { } end)
                {
                    documents = documents.Where(d => d.CreatedAt < end);
                }

                var groups = await documents.Join(_db.Set<DocumentType>().AsNoTracking(), d => d.DocumentTypeId, t => t.Id, (d, t) => new { t.Code, t.Name, t.IsSensitive, d.Status, d.SizeBytes }).GroupBy(x => new { x.Code, x.Name, x.IsSensitive, x.Status }).Select(g => new { g.Key.Code, g.Key.Name, g.Key.IsSensitive, g.Key.Status, Count = g.Count(), Size = g.Sum(x => x.SizeBytes) }).ToListAsync(cancellationToken);

                return Result<IReadOnlyList<DocumentStat>>.Ok([.. groups.Select(g => new DocumentStat(g.Code, g.Name, g.IsSensitive, g.Status, g.Count, g.Size))]);
            });

        public Task<Result<IReadOnlyList<DeliveryStat>>> GetDeliveryStatsAsync(DateTimeOffset from, DateTimeOffset toExclusive, CancellationToken cancellationToken = default) => ReadAsync<DeliveryStat>(async () =>
            {
                var groups = await _db.Set<NotificationDelivery>().AsNoTracking().Where(d => d.CreatedAt >= from && d.CreatedAt < toExclusive).GroupBy(d => new { d.Channel, d.Status }).Select(g => new { g.Key.Channel, g.Key.Status, Count = g.Count() }).ToListAsync(cancellationToken);

                return Result<IReadOnlyList<DeliveryStat>>.Ok([.. groups.Select(g => new DeliveryStat(g.Channel, g.Status, g.Count))]);
            });

        public Task<Result<IReadOnlyList<AuditStat>>> GetAuditStatsAsync(DateTimeOffset from, DateTimeOffset toExclusive, CancellationToken cancellationToken = default) => ReadAsync<AuditStat>(async () =>
            {
                var groups = await _db.Set<AuditLog>().AsNoTracking().Where(a => a.CreatedAt >= from && a.CreatedAt < toExclusive).GroupBy(a => a.Action).Select(g => new { Action = g.Key, Count = g.Count(), Last = g.Max(a => a.CreatedAt) }).ToListAsync(cancellationToken);

                return Result<IReadOnlyList<AuditStat>>.Ok([.. groups.Select(g => new AuditStat(g.Action, g.Count, g.Last))]);
            });

        // Database trouble is a Result, never an exception, and the details stay in the log.
        private async Task<Result<IReadOnlyList<T>>> ReadAsync<T>(Func<Task<Result<IReadOnlyList<T>>>> read)
        {
            try
            {
                return await read();
            }
            catch (OperationCanceledException)
            {
                return CommonErrors.OperationCancelled;
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("A report query failed ({ExceptionType}).", ex.GetType().Name);
                return ReportingErrors.DataUnavailable;
            }
        }
    }




}
