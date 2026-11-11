using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // Money that came in: payments per day and payment method, minus refunds.
    public sealed class FinanceCollectionsReport(IReportDataSource data) : IReportProvider
    {
        public const string Code = "FINANCE_COLLECTIONS";
        public const int MaxDays = 366;
        public const string TotalLabel = "Total";

        private static readonly ReportColumn[] Columns =
        [
            new("date", "Date", ReportColumnType.Date),
            new("method", "Method", ReportColumnType.Text),
            new("payments", "Payments", ReportColumnType.Integer),
            new("amount", "Collected", ReportColumnType.Decimal),
            new("refunded", "Refunded", ReportColumnType.Decimal),
            new("net", "Net", ReportColumnType.Decimal)
        ];

        private readonly IReportDataSource _data = data;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Collections",
            "Payments received per day and method, with refunds. Up to one year.",
            "Finance",
            Permissions.Finance.Read,
            false,
            [
                ReportParameters.From("Paid from", required: true),
                ReportParameters.To("Paid to", required: true),
                new("method", "Payment method", ReportParameterType.Choice, false, [.. Enum.GetNames<PaymentMethod>()])
            ],
            Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            var range = ReportRanges.Read(arguments, MaxDays);

            if (!range.TryGetValue(out var span) || span.From is not { } from || span.To is not { } to)
            {
                return range.IsFailure ? Result<ReportTable>.Fail(range.Errors) : Result<ReportTable>.Fail(Domain.Modules.Reporting.ReportingErrors.ParameterRequired("from"));
            }

            var facts = await _data.GetPaymentsAsync(from, to, cancellationToken);

            if (!facts.TryGetValue(out var payments))
            {
                return Result<ReportTable>.Fail(facts.Errors);
            }

            return ReportTable.Paged(Columns, Build(payments, arguments.Text("method")), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<PaymentFact> facts, string? method)
        {
            var wanted = method is null ? null : Enum.Parse<PaymentMethod>(method, ignoreCase: true) as PaymentMethod?;
            var selected = facts.Where(f => wanted is null || f.Method == wanted).ToList();
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var group in selected.GroupBy(f => (f.PaymentDate, f.Method)).OrderBy(g => g.Key.PaymentDate).ThenBy(g => g.Key.Method))
            {
                var amount = group.Sum(f => f.Amount);
                var refunded = group.Sum(f => f.Refunded);

                rows.Add(new object?[] { group.Key.PaymentDate, group.Key.Method.ToString(), group.Count(), Math.Round(amount, 2), Math.Round(refunded, 2), Math.Round(amount - refunded, 2) });
            }

            if (rows.Count > 0)
            {
                var amount = selected.Sum(f => f.Amount);
                var refunded = selected.Sum(f => f.Refunded);

                rows.Add(new object?[] { null, TotalLabel, selected.Count, Math.Round(amount, 2), Math.Round(refunded, 2), Math.Round(amount - refunded, 2) });
            }

            return rows;
        }
    }



}
