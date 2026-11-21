using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    internal static class AcademicReportSupport
    {
        public const string Category = "Academic";
        public const string TotalLabel = "Total";

        // Every fact sits in one department; the resolver asks the hierarchy once per distinct department.
        public static IEnumerable<ScopeRef> DepartmentScopes(IEnumerable<Guid> departmentIds)
            => departmentIds.Distinct().Select(id => new ScopeRef(ScopeType.Department, id));

        public static decimal Percent(int part, int whole) => whole == 0 ? 0m : Math.Round(part * 100m / whole, 2, MidpointRounding.AwayFromZero);
    }
}
