using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Scheduling.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Scheduling;

namespace UniCore.Application.Modules.Scheduling
{
    // The checks every change handler of this module starts with: the permission alone (Scheduling.Manage; schedules are
    // semester-wide, so there is no department scope), then load, then the semester must not be closed.
    public sealed class SchedulingAccess(IPermissionAuthorizer authorizer, IAggregateRepository<Schedule> schedules, IRepository<AcademicSemester> semesters)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IAggregateRepository<Schedule> _schedules = schedules;
        private readonly IRepository<AcademicSemester> _semesters = semesters;

        public Task<Result> RequireManageAsync(CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.Scheduling.Manage, null, cancellationToken);

        public Task<Result> RequireReadAsync(CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.Scheduling.Read, null, cancellationToken);

        // The semester must exist and (unless allowClosed) not be closed.
        public async Task<Result<AcademicSemester>> OpenSemesterAsync(Guid semesterId, bool allowClosed, CancellationToken cancellationToken)
        {
            var semester = semesterId == Guid.Empty ? null : await _semesters.GetByIdAsync(semesterId, cancellationToken);
            if (semester is null)
            {
                return SchedulingErrors.SemesterNotFound;
            }

            return semester.IsClosed && !allowClosed ? SchedulingErrors.SemesterClosed : semester;
        }

        // Manage permission -> load the schedule tracked, with its slots -> semester check.
        public async Task<Result<Schedule>> LoadForChangeAsync(Guid scheduleId, bool allowClosedSemester, CancellationToken cancellationToken)
        {
            var permission = await RequireManageAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return Result<Schedule>.Fail(permission.Errors);
            }

            var schedule = await _schedules.FirstOrDefaultAsync(new ScheduleByIdSpecification(scheduleId, tracking: true), cancellationToken);
            if (schedule is null)
            {
                return SchedulingErrors.ScheduleNotFound;
            }

            var semester = await OpenSemesterAsync(schedule.AcademicSemesterId, allowClosedSemester, cancellationToken);

            return semester.IsFailure ? Result<Schedule>.Fail(semester.Errors) : schedule;
        }
    }
}
