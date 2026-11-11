using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // Invoices by status: how many, how much was billed, how much was paid, how much is still owed.
    public sealed class FinanceInvoiceSummaryReport(IReportDataSource data) : IReportProvider
    {
        public const string Code = "FINANCE_INVOICE_SUMMARY";
        public const string TotalLabel = "Total";

        private static readonly ReportColumn[] Columns =
        [
            new("status", "Status", ReportColumnType.Text),
            new("invoices", "Invoices", ReportColumnType.Integer),
            new("total", "Billed", ReportColumnType.Decimal),
            new("paid", "Paid", ReportColumnType.Decimal),
            new("balance", "Balance", ReportColumnType.Decimal)
        ];

        private readonly IReportDataSource _data = data;

        public ReportDescriptor Descriptor { get; } = new(Code, "Invoice summary", "Invoices by status: count, amount billed, amount paid and balance. Filter by issue date and status.", "Finance", Permissions.Finance.Read, false, [ReportParameters.From("Issued from"), ReportParameters.To("Issued to"), new("status", "Status", ReportParameterType.Choice, false, [.. Enum.GetNames<InvoiceStatus>()])], Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            var range = ReportRanges.Read(arguments);

            if (!range.TryGetValue(out var span))
            {
                return Result<ReportTable>.Fail(range.Errors);
            }

            var facts = await _data.GetInvoicesAsync(span.From, span.To, cancellationToken);

            if (!facts.TryGetValue(out var invoices))
            {
                return Result<ReportTable>.Fail(facts.Errors);
            }

            return ReportTable.Paged(Columns, Build(invoices, arguments.Text("status")), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<InvoiceFact> facts, string? status)
        {
            var wanted = status is null ? null : Enum.Parse<InvoiceStatus>(status, ignoreCase: true) as InvoiceStatus?;
            var selected = facts.Where(f => wanted is null || f.Status == wanted).ToList();
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var group in selected.GroupBy(f => f.Status).OrderBy(g => g.Key))
            {
                rows.Add(Row(group.Key.ToString(), group.Count(), group.Sum(f => f.Total), group.Sum(f => f.Paid), group.Sum(f => f.Balance)));
            }

            if (rows.Count > 0)
            {
                rows.Add(Row(TotalLabel, selected.Count, selected.Sum(f => f.Total), selected.Sum(f => f.Paid), selected.Sum(f => f.Balance)));
            }

            return rows;
        }

        private static object?[] Row(string status, int count, decimal total, decimal paid, decimal balance) => [status, count, Math.Round(total, 2), Math.Round(paid, 2), Math.Round(balance, 2)];
    }



}
