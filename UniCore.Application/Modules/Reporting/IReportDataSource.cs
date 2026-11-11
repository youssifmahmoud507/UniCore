using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Reporting
{
    // Everything the reports and the dashboard read. Implemented in Infrastructure with read-only queries.
    // A call that would return too much data fails with REPORT_SOURCE_TOO_LARGE instead of loading it.
    public interface IReportDataSource
    {
        Task<Result<IReadOnlyList<InvoiceFact>>> GetInvoicesAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<OpenInvoiceFact>>> GetOpenInvoicesAsync(CancellationToken cancellationToken = default);

        // Payments dated from the first day to the last day, both included.
        Task<Result<IReadOnlyList<PaymentFact>>> GetPaymentsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

        // Requests that STARTED in the range: from is included, toExclusive is not (both ends optional).
        Task<Result<IReadOnlyList<WorkflowFact>>> GetWorkflowsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default);

        // In progress and returned requests always; finished ones only when they finished on or after completedSince
        // (all of them when completedSince is null).
        Task<Result<IReadOnlyList<WorkflowStat>>> GetWorkflowStatsAsync(DateTimeOffset? completedSince, CancellationToken cancellationToken = default);

        // Documents created in the range: from is included, toExclusive is not (both ends optional).
        Task<Result<IReadOnlyList<DocumentStat>>> GetDocumentStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<DeliveryStat>>> GetDeliveryStatsAsync(DateTimeOffset from, DateTimeOffset toExclusive, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<AuditStat>>> GetAuditStatsAsync(DateTimeOffset from, DateTimeOffset toExclusive, CancellationToken cancellationToken = default);
    }




}
