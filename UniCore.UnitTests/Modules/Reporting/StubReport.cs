using UniCore.Application.Modules.Reporting;
using UniCore.Domain.Common.Results;

namespace UniCore.UnitTests.Modules.Reporting
{
    // A report with a fixed number of rows. Remembers how it was asked.
    internal sealed class StubReport(string code, string permission, int rows = 5, bool sensitive = false, IReadOnlyList<ReportParameter>? parameters = null) : IReportProvider
    {
        public ReportDescriptor Descriptor { get; } = new(
            code,
            $"Report {code}",
            "A stub report",
            "Test",
            permission,
            sensitive,
            parameters ?? [new ReportParameter("from", "From", ReportParameterType.Date), new ReportParameter("status", "Status", ReportParameterType.Choice, false, ["Open", "Closed"])],
            [new ReportColumn("n", "Number", ReportColumnType.Integer), new ReportColumn("name", "Name", ReportColumnType.Text)]);

        public ReportArguments? LastArguments { get; private set; }
        public ReportPage? LastPage { get; private set; }
        public Error? Fail { get; set; }

        public Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            LastArguments = arguments;
            LastPage = page;

            if (Fail is { } error)
            {
                return Task.FromResult(Result<ReportTable>.Fail(error));
            }

            var all = Enumerable.Range(1, rows).Select(i => (IReadOnlyList<object?>)new object?[] { i, $"row {i}" }).ToList();

            return Task.FromResult(Result<ReportTable>.Ok(ReportTable.Paged(Descriptor.Columns, all, page)));
        }
    }




}
