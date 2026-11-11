using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Dashboard
{
    public sealed class FinanceDashboardWidget(IReportDataSource data, IClock clock) : IDashboardWidget
    {
        private readonly IReportDataSource _data = data;
        private readonly IClock _clock = clock;

        public string Code => "finance";

        public string Title => "Finance";

        public string Category => "Finance";

        public string Permission => Permissions.Finance.Read;

        public bool IsPerUser => false;

        public async Task<Result<IReadOnlyList<DashboardMetric>>> LoadAsync(CancellationToken cancellationToken = default)
        {
            var today = _clock.UtcToday;
            var open = await _data.GetOpenInvoicesAsync(cancellationToken);

            if (!open.TryGetValue(out var invoices))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(open.Errors);
            }

            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var payments = await _data.GetPaymentsAsync(monthStart, today, cancellationToken);

            if (!payments.TryGetValue(out var collected))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(payments.Errors);
            }

            return Result<IReadOnlyList<DashboardMetric>>.Ok(Build(invoices, collected, today));
        }

        public static IReadOnlyList<DashboardMetric> Build(IReadOnlyList<OpenInvoiceFact> open, IReadOnlyList<PaymentFact> paymentsThisMonth, DateOnly today)
            =>
            [
                new("openInvoices", "Open invoices", open.Count),
                new("outstanding", "Outstanding balance", Math.Round(open.Sum(i => i.Balance), 2)),
                new("overdueInvoices", "Overdue invoices", open.Count(i => i.DueDate < today)),
                new("collectedThisMonth", "Collected this month", Math.Round(paymentsThisMonth.Sum(p => p.Amount - p.Refunded), 2))
            ];
    }


}
