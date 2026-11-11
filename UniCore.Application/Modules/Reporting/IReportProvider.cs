using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Reporting
{
    // One report. The handler has already checked Reports.Read, the report's own permission and the arguments; a
    // provider that shows scoped data (a dean sees only the faculty) still filters by the user's scopes itself.
    public interface IReportProvider
    {
        ReportDescriptor Descriptor { get; }

        Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default);
    }




}
