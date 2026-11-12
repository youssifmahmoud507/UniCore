using UniCore.Application.Modules.Reporting;
using UniCore.Application.Modules.Reporting.Reports;
using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Reporting
{
    public class FinanceReportsTests
    {
        private static readonly DateOnly Today = new(2026, 11, 8);

        private static InvoiceFact Invoice(string student, InvoiceStatus status, decimal total, decimal paid, DateOnly? due = null, Guid? studentId = null)
            => new(studentId ?? Guid.NewGuid(), student, new DateOnly(2026, 9, 1), due ?? new DateOnly(2026, 10, 1), status, total, paid, total - paid);

        [Fact]
        public void The_invoice_summary_groups_by_status_and_adds_a_total_row()
        {
            var rows = FinanceInvoiceSummaryReport.Build(
            [
                Invoice("S1", InvoiceStatus.Paid, 1000m, 1000m),
                Invoice("S2", InvoiceStatus.Paid, 500m, 500m),
                Invoice("S3", InvoiceStatus.PartiallyPaid, 800m, 300m),
                Invoice("S4", InvoiceStatus.Issued, 200m, 0m)
            ], null);

            Assert.Equal(4, rows.Count);
            Assert.Equal(new object?[] { "Issued", 1, 200m, 0m, 200m }, rows[0].ToArray());
            Assert.Equal(new object?[] { "PartiallyPaid", 1, 800m, 300m, 500m }, rows[1].ToArray());
            Assert.Equal(new object?[] { "Paid", 2, 1500m, 1500m, 0m }, rows[2].ToArray());
            Assert.Equal(new object?[] { "Total", 4, 2500m, 1800m, 700m }, rows[3].ToArray());
        }

        [Fact]
        public void The_invoice_summary_can_be_limited_to_one_status()
        {
            var rows = FinanceInvoiceSummaryReport.Build([Invoice("S1", InvoiceStatus.Paid, 100m, 100m), Invoice("S2", InvoiceStatus.Issued, 50m, 0m)], "Issued");

            Assert.Equal(2, rows.Count);
            Assert.Equal("Issued", rows[0][0]);
            Assert.Equal(50m, rows[1][2]);
        }

        [Fact]
        public void With_no_invoices_the_summary_is_empty_and_has_no_total_row()
            => Assert.Empty(FinanceInvoiceSummaryReport.Build([], null));

        [Fact]
        public void Amounts_are_rounded_to_two_decimals()
        {
            var rows = FinanceInvoiceSummaryReport.Build([Invoice("S1", InvoiceStatus.Issued, 10.005m, 0m), Invoice("S2", InvoiceStatus.Issued, 10.004m, 0m)], null);

            Assert.Equal(20.01m, rows[0][2]);
        }

        [Fact]
        public void Outstanding_balances_add_up_open_invoices_per_student_biggest_first()
        {
            var ali = Guid.NewGuid();
            var rows = FinanceOutstandingBalancesReport.Build(
            [
                Invoice("S-ALI", InvoiceStatus.PartiallyPaid, 1000m, 400m, new DateOnly(2026, 10, 1), ali),
                Invoice("S-ALI", InvoiceStatus.Issued, 500m, 0m, new DateOnly(2026, 12, 1), ali),
                Invoice("S-SARA", InvoiceStatus.Issued, 2000m, 0m, new DateOnly(2026, 11, 20)),
                Invoice("S-PAID", InvoiceStatus.Paid, 300m, 300m),
                Invoice("S-GONE", InvoiceStatus.Cancelled, 300m, 0m)
            ], Today, false);

            Assert.Equal(2, rows.Count);
            Assert.Equal(new object?[] { "S-SARA", 1, 2000m, 0m, new DateOnly(2026, 11, 20), 0 }, rows[0].ToArray());
            Assert.Equal("S-ALI", rows[1][0]);
            Assert.Equal(2, rows[1][1]);
            Assert.Equal(1100m, rows[1][2]);
            Assert.Equal(600m, rows[1][3]);
            Assert.Equal(new DateOnly(2026, 10, 1), rows[1][4]);
            Assert.Equal(38, rows[1][5]);
        }

        [Fact]
        public void Overdue_only_keeps_students_with_an_overdue_balance()
        {
            var rows = FinanceOutstandingBalancesReport.Build(
            [
                Invoice("S-LATE", InvoiceStatus.Issued, 100m, 0m, new DateOnly(2026, 11, 1)),
                Invoice("S-OK", InvoiceStatus.Issued, 900m, 0m, new DateOnly(2026, 12, 1))
            ], Today, true);

            Assert.Single(rows);
            Assert.Equal("S-LATE", rows[0][0]);
        }

        [Fact]
        public void A_due_date_of_today_is_not_overdue_yet()
        {
            var rows = FinanceOutstandingBalancesReport.Build([Invoice("S1", InvoiceStatus.Issued, 100m, 0m, Today)], Today, true);

            Assert.Empty(rows);
        }

        [Fact]
        public void Collections_group_per_day_and_method_with_refunds_and_a_total()
        {
            var rows = FinanceCollectionsReport.Build(
            [
                new PaymentFact(new DateOnly(2026, 11, 2), PaymentMethod.Cash, 500m, 0m),
                new PaymentFact(new DateOnly(2026, 11, 2), PaymentMethod.Cash, 250m, 50m),
                new PaymentFact(new DateOnly(2026, 11, 2), PaymentMethod.Card, 100m, 0m),
                new PaymentFact(new DateOnly(2026, 11, 1), PaymentMethod.BankTransfer, 1000m, 0m)
            ], null);

            Assert.Equal(4, rows.Count);
            Assert.Equal(new object?[] { new DateOnly(2026, 11, 1), "BankTransfer", 1, 1000m, 0m, 1000m }, rows[0].ToArray());
            Assert.Equal(new object?[] { new DateOnly(2026, 11, 2), "Cash", 2, 750m, 50m, 700m }, rows[1].ToArray());
            Assert.Equal(new object?[] { new DateOnly(2026, 11, 2), "Card", 1, 100m, 0m, 100m }, rows[2].ToArray());
            Assert.Equal(new object?[] { null, "Total", 4, 1850m, 50m, 1800m }, rows[3].ToArray());
        }

        [Fact]
        public void Collections_can_be_limited_to_one_method()
        {
            var rows = FinanceCollectionsReport.Build([new PaymentFact(Today, PaymentMethod.Cash, 10m, 0m), new PaymentFact(Today, PaymentMethod.Card, 20m, 0m)], "Card");

            Assert.Equal(2, rows.Count);
            Assert.Equal("Card", rows[0][1]);
        }

        [Fact]
        public async Task Collections_need_a_range_of_at_most_a_year()
        {
            var data = new FakeReportData();
            var report = new FinanceCollectionsReport(data);

            var tooWide = await report.RunAsync(new ReportArguments(new Dictionary<string, object> { ["from"] = new DateOnly(2025, 1, 1), ["to"] = new DateOnly(2026, 6, 1) }), ReportPage.First(10));
            var backwards = await report.RunAsync(new ReportArguments(new Dictionary<string, object> { ["from"] = new DateOnly(2026, 2, 1), ["to"] = new DateOnly(2026, 1, 1) }), ReportPage.First(10));
            var exactlyAYear = await report.RunAsync(new ReportArguments(new Dictionary<string, object> { ["from"] = new DateOnly(2025, 1, 1), ["to"] = new DateOnly(2025, 12, 31) }), ReportPage.First(10));

            Assert.Equal("REPORT_DATE_RANGE_TOO_WIDE", tooWide.Errors[0].Code);
            Assert.Equal("REPORT_DATE_RANGE_INVALID", backwards.Errors[0].Code);
            Assert.True(exactlyAYear.IsSuccess);
            Assert.Equal(new DateOnly(2025, 1, 1), data.PaymentRange.From);
            Assert.Equal(new DateOnly(2025, 12, 31), data.PaymentRange.To);
        }

        [Fact]
        public async Task The_invoice_summary_passes_the_range_to_the_data_source_and_the_total_row_is_paged_with_the_rest()
        {
            var data = new FakeReportData();
            data.Invoices.Add(Invoice("S1", InvoiceStatus.Issued, 10m, 0m));
            var report = new FinanceInvoiceSummaryReport(data);

            var ran = await report.RunAsync(new ReportArguments(new Dictionary<string, object> { ["from"] = new DateOnly(2026, 1, 1) }), new ReportPage(0, 1));

            Assert.True(ran.TryGetValue(out var table));
            Assert.Equal(new DateOnly(2026, 1, 1), data.InvoiceRange.From);
            Assert.Null(data.InvoiceRange.To);
            Assert.Equal(2, table!.TotalRows);
            Assert.Single(table.Rows);
        }

        [Fact]
        public async Task A_data_source_failure_is_returned_as_it_is()
        {
            var data = new FakeReportData { Fail = UniCore.Domain.Modules.Reporting.ReportingErrors.SourceTooLarge };

            var ran = await new FinanceInvoiceSummaryReport(data).RunAsync(ReportArguments.None, ReportPage.First(10));

            Assert.Equal("REPORT_SOURCE_TOO_LARGE", ran.Errors[0].Code);
        }
    }




}
