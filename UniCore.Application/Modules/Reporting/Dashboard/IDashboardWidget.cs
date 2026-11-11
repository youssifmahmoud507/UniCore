using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Reporting.Dashboard
{
    // A block of numbers on the dashboard. Shown to users who hold Reports.Read and the widget's permission.
    public interface IDashboardWidget
    {
        string Code { get; }

        string Title { get; }

        string Category { get; }

        string Permission { get; }

        // True when the numbers depend on the user's scopes: then each user has their own cached copy.
        bool IsPerUser { get; }

        Task<Result<IReadOnlyList<DashboardMetric>>> LoadAsync(CancellationToken cancellationToken = default);
    }


}
