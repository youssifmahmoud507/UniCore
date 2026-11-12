using UniCore.Application.Modules.Reporting;
using UniCore.Domain.Common.Results;

namespace UniCore.UnitTests.Modules.Reporting
{
    internal sealed class FakeReportData : IReportDataSource
    {
        public List<InvoiceFact> Invoices { get; } = [];
        public List<OpenInvoiceFact> OpenInvoices { get; } = [];
        public List<PaymentFact> Payments { get; } = [];
        public List<WorkflowFact> Workflows { get; } = [];
        public List<WorkflowStat> WorkflowStats { get; } = [];
        public List<DocumentStat> Documents { get; } = [];
        public List<DeliveryStat> Deliveries { get; } = [];
        public List<AuditStat> Audit { get; } = [];
        public Error? Fail { get; set; }

        // What the last call asked for.
        public (DateOnly? From, DateOnly? To) InvoiceRange { get; private set; }
        public (DateOnly From, DateOnly To) PaymentRange { get; private set; }
        public (DateTimeOffset? From, DateTimeOffset? ToExclusive) WorkflowRange { get; private set; }
        public (DateTimeOffset From, DateTimeOffset ToExclusive) AuditRange { get; private set; }
        public (DateTimeOffset From, DateTimeOffset ToExclusive) DeliveryRange { get; private set; }
        public DateTimeOffset? CompletedSince { get; private set; }
        public int Calls { get; private set; }

        private Result<IReadOnlyList<T>> Answer<T>(List<T> rows)
        {
            Calls++;
            return Fail is { } error ? Result<IReadOnlyList<T>>.Fail(error) : Result<IReadOnlyList<T>>.Ok([.. rows]);
        }

        public Task<Result<IReadOnlyList<InvoiceFact>>> GetInvoicesAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        {
            InvoiceRange = (from, to);
            return Task.FromResult(Answer(Invoices));
        }

        public Task<Result<IReadOnlyList<OpenInvoiceFact>>> GetOpenInvoicesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Answer(OpenInvoices));

        public Task<Result<IReadOnlyList<PaymentFact>>> GetPaymentsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        {
            PaymentRange = (from, to);
            return Task.FromResult(Answer(Payments));
        }

        public Task<Result<IReadOnlyList<WorkflowFact>>> GetWorkflowsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default)
        {
            WorkflowRange = (from, toExclusive);
            return Task.FromResult(Answer(Workflows));
        }

        public Task<Result<IReadOnlyList<WorkflowStat>>> GetWorkflowStatsAsync(DateTimeOffset? completedSince, CancellationToken cancellationToken = default)
        {
            CompletedSince = completedSince;
            return Task.FromResult(Answer(WorkflowStats));
        }

        public Task<Result<IReadOnlyList<DocumentStat>>> GetDocumentStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default) => Task.FromResult(Answer(Documents));

        public Task<Result<IReadOnlyList<DeliveryStat>>> GetDeliveryStatsAsync(DateTimeOffset from, DateTimeOffset toExclusive, CancellationToken cancellationToken = default)
        {
            DeliveryRange = (from, toExclusive);
            return Task.FromResult(Answer(Deliveries));
        }

        public Task<Result<IReadOnlyList<AuditStat>>> GetAuditStatsAsync(DateTimeOffset from, DateTimeOffset toExclusive, CancellationToken cancellationToken = default)
        {
            AuditRange = (from, toExclusive);
            return Task.FromResult(Answer(Audit));
        }
    }




}
