using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Reporting.Dashboard
{
    // Students by status and the withdrawal and leave requests waiting for a decision, in the user's scope.
    public sealed class AcademicDashboardWidget(IReportDataSource data, IReportScopeResolver scopes) : IDashboardWidget
    {
        private readonly IReportDataSource _data = data;
        private readonly IReportScopeResolver _scopes = scopes;

        public string Code => "academic";

        public string Title => "Students and requests";

        public string Category => "Students";

        public string Permission => Permissions.Students.Read;

        public bool IsPerUser => true;

        public async Task<Result<IReadOnlyList<DashboardMetric>>> LoadAsync(CancellationToken cancellationToken = default)
        {
            var statuses = await _data.GetStudentStatusStatsAsync(cancellationToken);

            if (!statuses.TryGetValue(out var students))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(statuses.Errors);
            }

            var requests = await _data.GetPendingAcademicRequestsAsync(cancellationToken);

            if (!requests.TryGetValue(out var pending))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(requests.Errors);
            }

            var departments = students.Select(s => s.DepartmentId).Concat(pending.Select(p => p.DepartmentId)).Where(d => d is { }).Select(d => d!.Value).Distinct();
            var scope = await _scopes.ResolveAsync(Permissions.Students.Read, departments.Select(id => new ScopeRef(ScopeType.Department, id)), cancellationToken);

            return Result<IReadOnlyList<DashboardMetric>>.Ok(Build(students, pending, scope));
        }

        public static IReadOnlyList<DashboardMetric> Build(IEnumerable<StudentStatusStat> students, IEnumerable<AcademicRequestStat> pending, ReportScope scope)
        {
            var visibleStudents = students.Where(s => scope.Allows(s.DepartmentId)).ToList();
            var visibleRequests = pending.Where(p => scope.Allows(p.DepartmentId)).ToList();

            int Students(StudentAcademicStatus status) => visibleStudents.Where(s => s.Status == status).Sum(s => s.Count);

            int Requests(string kind) => visibleRequests.Where(p => p.Kind == kind).Sum(p => p.Count);

            return
            [
                new("students", "Students", visibleStudents.Sum(s => s.Count)),
                new("active", "Active students", Students(StudentAcademicStatus.Active)),
                new("onLeave", "Students on leave", Students(StudentAcademicStatus.OnLeave)),
                new("pendingWithdrawals", "Withdrawal requests waiting", Requests("Withdrawal")),
                new("pendingLeaves", "Leave requests waiting", Requests("Leave"))
            ];
        }
    }
}
