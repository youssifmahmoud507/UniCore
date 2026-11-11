using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // Who owes money: open invoices added up per student, biggest balance first.
    public sealed class FinanceOutstandingBalancesReport(IReportDataSource data, IClock clock) : IReportProvider
    {
        public const string Code = "FINANCE_OUTSTANDING_BALANCES";

        private static readonly ReportColumn[] Columns =
        [
            new("studentNumber", "Student number", ReportColumnType.Text),
            new("openInvoices", "Open invoices", ReportColumnType.Integer),
            new("balance", "Balance", ReportColumnType.Decimal),
            new("overdueBalance", "Overdue balance", ReportColumnType.Decimal),
            new("oldestDueDate", "Oldest due date", ReportColumnType.Date),
            new("daysOverdue", "Days overdue", ReportColumnType.Integer)
        ];

        private readonly IReportDataSource _data = data;
        private readonly IClock _clock = clock;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Outstanding balances",
            "Students with unpaid invoices, biggest balance first. Names student numbers and amounts, so every run is audited.",
            "Finance",
            Permissions.Finance.Read,
            true,
            [new("overdueOnly", "Overdue only", ReportParameterType.Boolean, false, Description: "true keeps only students with an overdue balance")],
            Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            var facts = await _data.GetInvoicesAsync(null, null, cancellationToken);

            if (!facts.TryGetValue(out var invoices))
            {
                return Result<ReportTable>.Fail(facts.Errors);
            }

            return ReportTable.Paged(Columns, Build(invoices, _clock.UtcToday, arguments.Boolean("overdueOnly") == true), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<InvoiceFact> facts, DateOnly today, bool overdueOnly)
        {
            var open = facts.Where(f => (f.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid) && f.Balance > 0m);

            var students = open
                .GroupBy(f => f.StudentId)
                .Select(g =>
                {
                    var overdue = g.Where(f => f.DueDate < today).ToList();
                    var oldestOverdue = overdue.Count == 0 ? (DateOnly?)null : overdue.Min(f => f.DueDate);

                    return new
                    {
                        Number = g.First().StudentNumber,
                        Open = g.Count(),
                        Balance = Math.Round(g.Sum(f => f.Balance), 2),
                        Overdue = Math.Round(overdue.Sum(f => f.Balance), 2),
                        Oldest = g.Min(f => f.DueDate),
                        DaysOverdue = oldestOverdue is { } due ? today.DayNumber - due.DayNumber : 0
                    };
                }).Where(s => !overdueOnly || s.Overdue > 0m).OrderByDescending(s => s.Balance).ThenBy(s => s.Number, StringComparer.Ordinal);

            return [.. students.Select(s => (IReadOnlyList<object?>)new object?[] { s.Number, s.Open, s.Balance, s.Overdue, s.Oldest, s.DaysOverdue })];
        }
    }



}
